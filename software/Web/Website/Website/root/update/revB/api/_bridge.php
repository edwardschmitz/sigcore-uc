<?php
declare(strict_types=1);

const SIGCORE_BRIDGE_BASE = 'http://127.0.0.1:8080';

function bridge_get(string $path, int $timeoutMs = 1500): array
{
    $url = SIGCORE_BRIDGE_BASE . $path;

    if (function_exists('curl_init')) {
        $ch = curl_init($url);

        curl_setopt_array($ch, [
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_CONNECTTIMEOUT_MS => 500,
            CURLOPT_TIMEOUT_MS => $timeoutMs,
            CURLOPT_HTTPHEADER => [
                'Accept: application/json'
            ],
        ]);

        $body = curl_exec($ch);

        if ($body === false) {
            $error = curl_error($ch);
            curl_close($ch);

            return [
                'ok' => false,
                'status' => 502,
                'body' => json_encode([
                    'error' => 'WebBridge unavailable.',
                    'detail' => $error
                ])
            ];
        }

        $status = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        return [
            'ok' => $status >= 200 && $status < 300,
            'status' => $status > 0 ? $status : 502,
            'body' => $body
        ];
    }

    $context = stream_context_create([
        'http' => [
            'method' => 'GET',
            'timeout' => $timeoutMs / 1000,
            'ignore_errors' => true,
            'header' => "Accept: application/json\r\n"
        ]
    ]);

    $body = @file_get_contents($url, false, $context);

    if ($body === false) {
        return [
            'ok' => false,
            'status' => 502,
            'body' => json_encode([
                'error' => 'WebBridge unavailable.'
            ])
        ];
    }

    $status = 200;

    if (isset($http_response_header) && is_array($http_response_header)) {
        foreach ($http_response_header as $header) {
            if (preg_match('/^HTTP\/\S+\s+(\d{3})/', $header, $matches)) {
                $status = (int)$matches[1];
                break;
            }
        }
    }

    return [
        'ok' => $status >= 200 && $status < 300,
        'status' => $status,
        'body' => $body
    ];
}

function emit_bridge_json(string $path): never
{
    $result = bridge_get($path);

    http_response_code($result['status']);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');

    echo $result['body'];
    exit;
}

// Forward write requests through the same loopback WebBridge connection as reads.
function bridge_request(string $method, string $path, ?string $body = null): array
{
    if ($method === 'GET') {
        return bridge_get($path, str_starts_with($path, '/api/device-settings/') ? 12000 : 1500);
    }

    if (!in_array($method, ['POST', 'PUT'], true)) {
        return ['ok' => false, 'status' => 405,
            'body' => json_encode(['error' => 'Method not allowed.'])];
    }

    $headers = ['Accept: application/json', 'Content-Type: application/json'];
    $url = SIGCORE_BRIDGE_BASE . $path;
    if (function_exists('curl_init')) {
        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_CUSTOMREQUEST => $method,
            CURLOPT_POSTFIELDS => $body ?? '',
            CURLOPT_CONNECTTIMEOUT_MS => 500,
            CURLOPT_TIMEOUT_MS => 5000,
            CURLOPT_HTTPHEADER => $headers,
        ]);
        $response = curl_exec($ch);
        if ($response === false) {
            curl_close($ch);
            return ['ok' => false, 'status' => 502,
                'body' => json_encode(['error' => 'WebBridge unavailable.'])];
        }
        $status = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        return ['ok' => $status >= 200 && $status < 300,
            'status' => $status ?: 502, 'body' => $response];
    }

    $context = stream_context_create(['http' => [
        'method' => $method, 'timeout' => 5.0, 'ignore_errors' => true,
        'header' => implode("\r\n", $headers) . "\r\n",
        'content' => $body ?? '',
    ]]);
    $response = @file_get_contents($url, false, $context);
    if ($response === false) {
        return ['ok' => false, 'status' => 502,
            'body' => json_encode(['error' => 'WebBridge unavailable.'])];
    }
    $status = 502;
    foreach ($http_response_header ?? [] as $header) {
        if (preg_match('/^HTTP\/\S+\s+(\d{3})/', $header, $matches)) {
            $status = (int)$matches[1];
            break;
        }
    }
    return ['ok' => $status >= 200 && $status < 300,
        'status' => $status, 'body' => $response];
}

function emit_bridge_request(string $method, string $path): never
{
    $body = $method === 'GET' ? null : file_get_contents('php://input');
    $result = bridge_request($method, $path, $body === false ? '' : $body);
    http_response_code($result['status']);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    echo $result['body'];
    exit;
}
