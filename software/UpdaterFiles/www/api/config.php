<?php
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
require_once __DIR__ . '/_input-validation.php';
$method = $_SERVER['REQUEST_METHOD'] ?? '';
if (!in_array($method, ['GET', 'PUT'], true)) {
    http_response_code(405);
    header('Allow: GET, PUT');
    exit;
}
$kind = $_GET['kind'] ?? '';
$channel = filter_input(INPUT_GET, 'channel', FILTER_VALIDATE_INT);
$limits = ['relay' => 8, 'di' => 8, 'ai' => 4, 'ao' => 4, 'pid' => 4];
if (!is_string($kind) || !isset($limits[$kind]) || $channel === null || $channel === false
    || $channel < 0 || $channel >= $limits[$kind]) {
    http_response_code(400);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode(['error' => 'Invalid configuration target.']);
    exit;
}
if ($method === 'GET') {
    emit_bridge_request('GET', '/api/config/' . $kind . '/' . $channel);
}
$raw = file_get_contents('php://input');
$payload = decode_config_input($raw === false ? '' : $raw);
if (!isset($payload['config']) || !is_array($payload['config']) || array_is_list($payload['config'])) {
    reject_invalid_input('Missing configuration object.');
}
validate_channel_config_input($kind, $payload['config']);
$result = bridge_request('PUT', '/api/config/' . $kind . '/' . $channel, $raw);
http_response_code($result['status']);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
echo $result['body'];
exit;
