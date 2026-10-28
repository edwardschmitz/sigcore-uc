<?php
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';

if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
    http_response_code(405);
    header('Allow: GET');
    exit;
}

$result = bridge_get('/api/config-backup', 10000);
http_response_code($result['status']);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
if ($result['ok']) {
    header('Content-Disposition: attachment; filename="SigCore-backup.json"');
}
echo $result['body'];
