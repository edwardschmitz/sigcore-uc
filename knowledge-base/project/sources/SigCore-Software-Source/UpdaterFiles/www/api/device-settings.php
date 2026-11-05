<?php
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
$method = $_SERVER['REQUEST_METHOD'] ?? '';
$kind = $_GET['kind'] ?? '';
if (!in_array($kind, ['system','network','logger'], true) || !in_array($method, ['GET','PUT'], true)) {
    http_response_code(400);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode(['error' => 'Invalid device settings request.']);
    exit;
}
// The SigCore database logger cannot record failures that occur in Apache/PHP.
// Record only routing and HTTP status, never request bodies or credentials.
register_shutdown_function(static function () use ($kind, $method): void {
    $last = error_get_last();
    if ($last !== null && in_array($last['type'], [E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR, E_USER_ERROR], true)) {
        error_log(sprintf('[SigCore device-settings] PHP fatal: kind=%s method=%s type=%d file=%s line=%d',
            $kind, $method, $last['type'], basename($last['file']), $last['line']));
    }
});
error_log(sprintf('[SigCore device-settings] request: kind=%s method=%s', $kind, $method));
try {
    $path = '/api/device-settings/' . $kind;
    $body = $method === 'GET' ? null : file_get_contents('php://input');
    $result = bridge_request($method, $path, $body === false ? '' : $body);
    $status = (int)($result['status'] ?? 502);
    $responseBody = (string)($result['body'] ?? '');
    if ($status >= 500) {
        error_log(sprintf('[SigCore device-settings] upstream error: kind=%s method=%s path=%s status=%d responseType=%s bytes=%d',
            $kind, $method, $path, $status,
            json_decode($responseBody) === null ? 'non-JSON' : 'JSON', strlen($responseBody)));
    }
    http_response_code($status);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    if (json_decode($responseBody) === null) {
        // Preserve status and identify the failing boundary without leaking upstream error pages.
        echo json_encode(['error' => 'WebBridge returned an invalid response (HTTP ' . $status . ').']);
    } else {
        echo $responseBody;
    }
} catch (Throwable $error) {
    error_log(sprintf('[SigCore device-settings] PHP exception: kind=%s method=%s class=%s file=%s line=%d message=%s',
        $kind, $method, get_class($error), basename($error->getFile()), $error->getLine(), $error->getMessage()));
    http_response_code(500);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode(['error' => 'Device settings proxy failed.']);
}
exit;
