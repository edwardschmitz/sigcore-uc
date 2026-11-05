<?php
declare(strict_types=1);
require_once __DIR__ . '/_bridge.php';
// Core network configuration getter can take longer than live-state requests.
emit_bridge_json('/api/network', 12000);
