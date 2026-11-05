<?php
header("Access-Control-Allow-Origin: *");
header("Content-Type: application/json");

// Read input
$raw = file_get_contents("php://input");
$data = json_decode($raw, true);

// Validate JSON
if (!is_array($data)) {
    http_response_code(400);
    echo json_encode(["error" => "Invalid JSON"]);
    exit;
}

// Optional: Sort descending by date
usort($data, function($a, $b) {
    return strtotime($b['date']) - strtotime($a['date']);
});

// Write file
if (file_put_contents("media.json", json_encode($data, JSON_PRETTY_PRINT))) {
    echo json_encode(["success" => true]);
} else {
    http_response_code(500);
    echo json_encode(["error" => "Failed to save file"]);
}
