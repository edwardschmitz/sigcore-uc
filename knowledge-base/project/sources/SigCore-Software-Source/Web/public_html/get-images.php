<?php
error_reporting(E_ALL);
ini_set('display_errors', 1);

$folderPath = "slideshow-images/"; // Folder where images are stored
$imageExtensions = ['jpg', 'jpeg', 'png', 'gif']; // Allowed file types
$images = [];

// Check if the folder exists
if (!is_dir($folderPath)) {
    die(json_encode(["error" => "Folder not found: $folderPath"]));
}

// Scan the folder
$files = scandir($folderPath);
foreach ($files as $file) {
    if ($file === '.' || $file === '..') continue;

    $fileExtension = pathinfo($file, PATHINFO_EXTENSION);
    if (in_array(strtolower($fileExtension), $imageExtensions)) {
        $images[] = $folderPath . $file;
    }
}

// Return JSON output
header('Content-Type: application/json');
echo json_encode($images, JSON_UNESCAPED_SLASHES);
?>
