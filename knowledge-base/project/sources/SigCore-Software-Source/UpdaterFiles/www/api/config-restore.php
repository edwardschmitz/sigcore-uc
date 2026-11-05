<?php
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') { http_response_code(405); header('Allow: POST'); exit; }
if (isset($_SERVER['CONTENT_LENGTH']) && (int)$_SERVER['CONTENT_LENGTH'] > 1048576) {
    http_response_code(413); header('Content-Type: application/json'); echo '{"error":"Backup exceeds 1 MB."}'; exit;
}
emit_bridge_request('POST', '/api/config-restore');
