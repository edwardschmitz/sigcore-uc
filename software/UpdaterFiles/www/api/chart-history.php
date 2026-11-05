<?php
// Chart history uses the existing Apache/PHP -> WebBridge loopback helper.
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
    http_response_code(405);
    header('Allow: GET');
    exit;
}
foreach (['start', 'length', 'samples'] as $name) {
    if (!isset($_GET[$name]) || !is_string($_GET[$name]) ||
        !preg_match('/^-?[0-9]{1,18}$/D', $_GET[$name])) {
        http_response_code(400);
        header('Content-Type: application/json; charset=utf-8');
        echo json_encode(['error' => 'Invalid ' . $name . '.']);
        exit;
    }
}
$path = '/api/chart/history?' . http_build_query([
    'start' => $_GET['start'],
    'length' => $_GET['length'],
    'samples' => $_GET['samples']
], '', '&', PHP_QUERY_RFC3986);
$result = bridge_get($path, 10000);
http_response_code((int) $result['status']);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
echo $result['body'];
