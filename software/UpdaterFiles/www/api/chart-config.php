<?php
// Chart configuration uses the existing Apache/PHP -> WebBridge helper.
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
require_once __DIR__ . '/_input-validation.php';
$method = $_SERVER['REQUEST_METHOD'] ?? '';
if (!in_array($method, ['GET', 'PUT'], true)) {
    http_response_code(405);
    header('Allow: GET, PUT');
    exit;
}
if ($method === 'GET') {
    emit_bridge_json('/api/chart/config');
}
$body = file_get_contents('php://input');
if ($body === false || strlen($body) > 16384) {
    http_response_code(400);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode(['error' => 'Chart configuration exceeds 16 KB.']);
    exit;
}
$data = decode_config_input($body);
validate_chart_config_input($data);
$result = bridge_request('PUT', '/api/chart/config', $body);
http_response_code((int) $result['status']);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
echo $result['body'];
