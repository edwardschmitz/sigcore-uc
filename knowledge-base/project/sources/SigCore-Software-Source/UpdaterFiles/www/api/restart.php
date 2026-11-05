<?php
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
    http_response_code(405);
    header('Allow: POST');
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode(['error' => 'Method not allowed.']);
    exit;
}
emit_bridge_request('POST', '/api/restart');
