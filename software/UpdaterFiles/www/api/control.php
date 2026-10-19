<?php
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
    http_response_code(405);
    header('Allow: POST');
    exit;
}
$operation = $_GET['operation'] ?? '';
if ($operation === 'relay' || $operation === 'analog-output') {
    emit_bridge_request('POST', '/api/control/' . $operation);
}
if ($operation === 'pid') {
    $channel = filter_input(INPUT_GET, 'channel', FILTER_VALIDATE_INT);
    $action = $_GET['action'] ?? '';
    $actions = ['setpoint', 'output', 'tolerance', 'auto', 'ramp-time', 'ramp-target', 'ramp', 'reset'];
    if ($channel !== null && $channel !== false && $channel >= 0 && $channel <= 3
        && is_string($action) && in_array($action, $actions, true)) {
        emit_bridge_request('POST', '/api/control/pid/' . $channel . '/' . $action);
    }
}
http_response_code(400);
header('Content-Type: application/json; charset=utf-8');
echo json_encode(['error' => 'Unsupported control operation.']);
