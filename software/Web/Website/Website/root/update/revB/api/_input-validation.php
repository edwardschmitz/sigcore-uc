<?php
declare(strict_types=1);

// Validate website writes before they reach WebBridge. Core validation is still
// required because clients can also communicate with Core directly.
function reject_invalid_input(string $message): never
{
    http_response_code(400);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    echo json_encode(['error' => $message]);
    exit;
}

function decode_config_input(string $raw): array
{
    try {
        $payload = json_decode($raw, true, 512, JSON_THROW_ON_ERROR);
    } catch (JsonException $error) {
        reject_invalid_input('Request must contain valid JSON.');
    }
    if (!is_array($payload) || array_is_list($payload)) {
        reject_invalid_input('Request must contain a JSON object.');
    }
    return $payload;
}

function check_numeric_input(array $data, string $key, string $label,
                             bool $integer = false, ?float $min = null, ?float $max = null): void
{
    if (!array_key_exists($key, $data)) return;
    $value = $data[$key];
    if ((!is_int($value) && !is_float($value)) || !is_finite((float)$value) ||
        ($integer && (!is_int($value) && floor($value) !== $value)) ||
        ($min !== null && $value < $min) || ($max !== null && $value > $max)) {
        $range = $min !== null && $max !== null ? " from $min to $max" :
            ($min !== null ? " of at least $min" : ($max !== null ? " of at most $max" : ''));
        reject_invalid_input($label . ' must be a finite ' . ($integer ? 'whole number' : 'number') . $range . '.');
    }
}

function check_calibration_input(array $config): void
{
    $type = $config['calType'] ?? null;
    if ($type === 3 && array_key_exists('polyString', $config)) {
        $text = $config['polyString'];
        if (!is_string($text) || trim($text) === '')
            reject_invalid_input('Polynomial calibration requires numeric coefficients.');
        foreach (explode(',', $text) as $part) {
            if (trim($part) === '' || !is_numeric(trim($part)) || !is_finite((float)trim($part)))
                reject_invalid_input('Polynomial calibration must contain finite numeric coefficients.');
        }
    }
    if ($type === 2 && array_key_exists('pieceWiseString', $config)) {
        $text = $config['pieceWiseString'];
        if (!is_string($text)) reject_invalid_input('Piecewise calibration must contain input:output pairs.');
        $points = explode(',', $text);
        if (count($points) < 2) reject_invalid_input('Piecewise calibration requires at least two points.');
        $inputs = [];
        foreach ($points as $pair) {
            $parts = explode(':', $pair);
            if (count($parts) !== 2) reject_invalid_input('Piecewise calibration must contain valid input:output pairs.');
            foreach ($parts as $number) {
                if (trim($number) === '' || !is_numeric(trim($number)) || !is_finite((float)trim($number)))
                    reject_invalid_input('Piecewise calibration values must be finite numbers.');
            }
            $value = (float)$parts[0];
            if (in_array($value, $inputs, true)) reject_invalid_input('Piecewise calibration inputs must be unique.');
            $inputs[] = $value;
        }
        for ($i = 1; $i < count($inputs); $i++) {
            if ($inputs[$i] <= $inputs[$i - 1])
                reject_invalid_input('Piecewise calibration inputs must be in increasing order.');
        }
    }
}

function validate_channel_config_input(string $kind, array $config): void
{
    switch ($kind) {
        case 'di':
            check_numeric_input($config, 'debounce', 'Debounce', true, 0.0, 2147483647.0);
            break;
        case 'ai':
            check_numeric_input($config, 'avgSamples', 'Averaging', true, 1.0, 100.0);
            check_numeric_input($config, 'precision', 'Precision', true, 0.0, 9.0);
            check_numeric_input($config, 'm', 'Calibration slope');
            check_numeric_input($config, 'b', 'Calibration offset');
            check_calibration_input($config);
            break;
        case 'ao':
            check_numeric_input($config, 'precision', 'Precision', true, 0.0, 9.0);
            check_numeric_input($config, 'voltM', 'Voltage scale');
            check_numeric_input($config, 'voltB', 'Voltage offset');
            break;
        case 'pid':
            foreach (['kp'=>'Kp', 'ki'=>'Ki', 'kd'=>'Kd',
                      'outputMin'=>'Output minimum', 'outputMax'=>'Output maximum',
                      'pvMin'=>'PV minimum', 'pvMax'=>'PV maximum'] as $key => $label) {
                check_numeric_input($config, $key, $label);
            }
            check_numeric_input($config, 'pvSource', 'PV source channel', true, 0.0, 3.0);
            check_numeric_input($config, 'outputDestination', 'Output destination channel', true, 0.0, 3.0);
            check_numeric_input($config, 'integralLimit', 'Integral limit', false, 0.0);
            foreach ([['outputMin','outputMax','Output'], ['pvMin','pvMax','PV']] as $range) {
                [$min, $max, $label] = $range;
                if (isset($config[$min], $config[$max]) && $config[$min] >= $config[$max])
                    reject_invalid_input("$label minimum must be less than $label maximum.");
            }
            break;
    }
}

function valid_ipv4(string $text): ?array
{
    $parts = explode('.', $text);
    if (count($parts) !== 4) return null;
    $result = [];
    foreach ($parts as $part) {
        if (!preg_match('/^(0|[1-9][0-9]{0,2})$/D', $part) || (int)$part > 255) return null;
        $result[] = (int)$part;
    }
    return $result;
}

function valid_subnet_mask(array $parts): bool
{
    $value = 0;
    foreach ($parts as $part) $value = (($value << 8) | $part);
    $bits = sprintf('%032b', $value);
    return preg_match('/^1+0*$/D', $bits) === 1;
}

function validate_device_settings_input(string $kind, array $data): void
{
    switch ($kind) {
        case 'system':
            check_numeric_input($data, 'analogSamplingRate', 'Analog sampling rate', true, 0.0, 2.0);
            check_numeric_input($data, 'pwm_frequency', 'PWM frequency', true, 1.0, 2147483647.0);
            break;
        case 'logger':
            check_numeric_input($data, 'intervalSec', 'Logging interval', false, 0.001, 2147483.0);
            check_numeric_input($data, 'port', 'Database port', true, 1.0, 65535.0);
            check_numeric_input($data, 'connectionTimeoutSec', 'Connection timeout', true, 1.0, 2147483647.0);
            check_numeric_input($data, 'commandTimeoutSec', 'Command timeout', true, 1.0, 2147483647.0);
            break;
        case 'network':
            foreach (['ethernet'=>'Ethernet', 'wifi'=>'Wi-Fi'] as $key => $label) {
                if (!array_key_exists($key, $data)) continue;
                $iface = $data[$key];
                if (!is_array($iface) || array_is_list($iface))
                    reject_invalid_input("$label configuration must be an object.");
                if (($iface['dhcpEnabled'] ?? false) === true) continue;
                // Unconfigured Wi-Fi is allowed when no SSID or static values exist.
                if ($key === 'wifi' && trim((string)($iface['ssid'] ?? '')) === '' &&
                    trim((string)($iface['ipAddress'] ?? '')) === '' &&
                    trim((string)($iface['subnetMask'] ?? '')) === '' &&
                    trim((string)($iface['gateway'] ?? '')) === '' &&
                    trim((string)($iface['dns'] ?? '')) === '') continue;
                foreach (['ipAddress'=>'IP address', 'subnetMask'=>'subnet mask',
                          'gateway'=>'gateway', 'dns'=>'DNS server'] as $field => $name) {
                    $value = $iface[$field] ?? '';
                    if (($field === 'gateway' || $field === 'dns') && $value === '') continue;
                    $address = is_string($value) ? valid_ipv4(trim($value)) : null;
                    if ($address === null || ($field === 'subnetMask' && !valid_subnet_mask($address)))
                        reject_invalid_input("$label $name must be a valid " .
                            ($field === 'subnetMask' ? 'contiguous IPv4 subnet mask.' : 'IPv4 address.'));
                }
            }
            break;
    }
}

function validate_chart_config_input(array $data): void
{
    check_numeric_input($data, 'recordIntervalSec', 'Recording interval', false, 0.1, 60.0);
    check_numeric_input($data, 'maxMemoryMiB', 'History memory', true, 16.0, 1024.0);
    if (!array_key_exists('display', $data)) return;
    $display = $data['display'];
    if (!is_array($display) || array_is_list($display))
        reject_invalid_input('Chart display configuration must be an object.');
    check_numeric_input($display, 'windowSeconds', 'Chart window length', false, 1.0, 315360000.0);
    check_numeric_input($display, 'min', 'Scale minimum');
    check_numeric_input($display, 'max', 'Scale maximum');
    if (isset($display['min'], $display['max']) && $display['min'] >= $display['max'])
        reject_invalid_input('Scale minimum must be less than scale maximum.');
}
