<?php
declare(strict_types=1);

const SIGCORE_BRIDGE_BASE = 'http://127.0.0.1:8080';

function bridge_get(string $path): array
{
    $url = SIGCORE_BRIDGE_BASE . $path;

    if (function_exists('curl_init')) {
        $ch = curl_init($url);

        curl_setopt_array($ch, [
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_CONNECTTIMEOUT_MS => 500,
            CURLOPT_TIMEOUT_MS => 1500,
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
            'timeout' => 1.5,
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
