<?php
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
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
emit_bridge_request($method, '/api/config/' . $kind . '/' . $channel);
