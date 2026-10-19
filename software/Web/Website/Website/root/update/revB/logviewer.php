<?php
declare(strict_types=1);

/*
 * SigCore Log Viewer
 * Uses the same database configuration as the device logger.
 */

$propertiesFile = '/opt/sigcore/properties.json';
// WebBridge exposes the RAM-disk text log on its loopback-only HTTP listener.
$textLogUrl = 'http://127.0.0.1:8080/api/logs';
$allowedSources = ['auto', 'mysql', 'text'];
$requestedSource = strtolower(trim((string)($_GET['logsource'] ?? 'auto')));
if (!in_array($requestedSource, $allowedSources, true)) $requestedSource = 'auto';
$activeSource = 'none';
$sourceNotice = '';


$allowedLevels = ['INFO', 'WARN', 'ERROR', 'LOGGER-DEBUG'];
$allowedOrders = ['asc', 'desc'];
$allowedLimits = [100, 250, 500, 1000];

$sessionId = trim($_GET['session'] ?? '');
$serial    = trim($_GET['serial'] ?? '');
$level     = strtoupper(trim($_GET['level'] ?? ''));
$app       = trim($_GET['app'] ?? '');
$source    = trim($_GET['source'] ?? '');
$category  = trim($_GET['category'] ?? '');
$event     = trim($_GET['event'] ?? '');
$message   = trim($_GET['message'] ?? '');
$order     = strtolower(trim($_GET['order'] ?? 'desc'));
$limit     = (int)($_GET['limit'] ?? 250);

if (!in_array($level, $allowedLevels, true)) {
    $level = '';
}
if (!in_array($order, $allowedOrders, true)) {
    $order = 'desc';
}
if (!in_array($limit, $allowedLimits, true)) {
    $limit = 250;
}

function loadMysqlLogs(string $propertiesFile, string $sessionId, string $serial,
    string $level, string $app, string $source, string $category, string $event,
    string $message, string $order, int $limit): array {
    $apps = $serials = $sessions = $rows = [];
    if (!is_readable($propertiesFile)) {
        throw new RuntimeException('Device configuration file is not readable.');
    }

    $properties = json_decode(
        (string)file_get_contents($propertiesFile),
        true,
        512,
        JSON_THROW_ON_ERROR
    );
    $logger = $properties['logger'] ?? null;
    if (!is_array($logger)) {
        throw new RuntimeException('Logger configuration is missing from device properties.');
    }

    $dbHost = trim((string)($logger['server'] ?? ''));
    $dbPort = (int)($logger['port'] ?? 3306);
    $dbName = trim((string)($logger['database'] ?? ''));
    $dbUser = trim((string)($logger['user'] ?? ''));
    $dbPass = (string)($logger['password'] ?? '');

    if ($dbHost === '' || $dbPort < 1 || $dbPort > 65535 ||
        $dbName === '' || $dbUser === '' || $dbPass === '') {
        throw new RuntimeException('Logger database settings are incomplete or invalid.');
    }

    $dsn = "mysql:host={$dbHost};port={$dbPort};dbname={$dbName};charset=utf8mb4";
    $pdo = new PDO($dsn, $dbUser, $dbPass, [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_EMULATE_PREPARES => false,
        PDO::ATTR_TIMEOUT => 1, // Fail fast when MySQL is unreachable.
    ]);

    $apps = $pdo->query("
        SELECT DISTINCT App
        FROM logrecord
        WHERE App IS NOT NULL AND App <> ''
        ORDER BY App
    ")->fetchAll(PDO::FETCH_COLUMN);

    $serials = $pdo->query("
        SELECT DISTINCT SerialNumber
        FROM logsession
        WHERE SerialNumber IS NOT NULL AND SerialNumber <> ''
        ORDER BY SerialNumber
    ")->fetchAll(PDO::FETCH_COLUMN);

    $sessions = $pdo->query("
        SELECT Id, StartedAt, HostName, SerialNumber
        FROM logsession
        ORDER BY Id DESC
        LIMIT 200
    ")->fetchAll();

    $where = [];
    $params = [];

    if ($sessionId !== '' && ctype_digit($sessionId)) {
        $where[] = 'lr.SessionId = :sessionId';
        $params[':sessionId'] = (int)$sessionId;
    }

    if ($serial !== '') {
        $where[] = 'ls.SerialNumber = :serial';
        $params[':serial'] = $serial;
    }

    if ($level !== '') {
        $where[] = 'lr.Level = :level';
        $params[':level'] = $level;
    }

    if ($app !== '') {
        $where[] = 'lr.App = :app';
        $params[':app'] = $app;
    }

    if ($source !== '') {
        $where[] = 'lr.Source LIKE :source';
        $params[':source'] = '%' . $source . '%';
    }

    if ($category !== '') {
        $where[] = 'lr.Category LIKE :category';
        $params[':category'] = '%' . $category . '%';
    }

    if ($event !== '') {
        $where[] = 'lr.Event LIKE :event';
        $params[':event'] = '%' . $event . '%';
    }

    if ($message !== '') {
        $where[] = 'lr.Message LIKE :message';
        $params[':message'] = '%' . $message . '%';
    }

    $sql = "
        SELECT
            lr.Id,
            lr.SessionId,
            lr.Timestamp,
            lr.Level,
            lr.App,
            lr.Source,
            lr.Category,
            lr.Event,
            lr.Message,
            ls.SerialNumber,
            ls.HostName
        FROM logrecord lr
        INNER JOIN logsession ls ON ls.Id = lr.SessionId
    ";

    if ($where) {
        $sql .= ' WHERE ' . implode(' AND ', $where);
    }

    $sql .= ' ORDER BY lr.Timestamp ' . strtoupper($order) . ', lr.Id ' . strtoupper($order);
    $sql .= ' LIMIT ' . $limit;

    $stmt = $pdo->prepare($sql);
    $stmt->execute($params);
    $rows = $stmt->fetchAll();

    return [$rows, $apps, $serials, $sessions];
}

function parseTextLogLine(string $line): ?array {
    // The logger writes ISO-style local timestamps, offsets, and a bracketed level.
    // A UTF-8 BOM may be present at the beginning of the file.
    $line = preg_replace('/^\xEF\xBB\xBF/', '', rtrim($line, "\r\n"));
    if (!preg_match('/^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+ [+-]\d{2}:\d{2}) \[([^\]]+)\] (.*)$/', $line, $m)) {
        return null;
    }

    $rest = $m[3];
    $fields = ['App' => '', 'Source' => '', 'Category' => '', 'Event' => ''];
    // Parse fixed-format fields in order. The message is free text and may
    // contain spaces, equals signs, and additional 'source=' or 'app=' text.
    if (preg_match('/^app=(\S+)(?: source=(\S+) category=(\S+) event=(\S+))? message=(.*)$/s', $rest, $parts)) {
        $fields['App'] = $parts[1];
        $fields['Source'] = $parts[2] ?? '';
        $fields['Category'] = $parts[3] ?? '';
        $fields['Event'] = $parts[4] ?? '';
        $rest = $parts[5];
    }

    return [
        'Id' => '', 'SessionId' => '', 'SerialNumber' => '', 'HostName' => '',
        'Timestamp' => $m[1], 'Level' => strtoupper($m[2]),
        'App' => $fields['App'], 'Source' => $fields['Source'],
        'Category' => $fields['Category'], 'Event' => $fields['Event'],
        'Message' => $rest,
    ];
}

function loadTextLogs(string $url, string $level, string $app, string $source,
    string $category, string $event, string $message, string $order, int $limit): array {
    $context = stream_context_create(['http' => [
        'method' => 'GET', 'timeout' => 5, 'ignore_errors' => true,
    ]]);
    $content = @file_get_contents($url, false, $context);
    if ($content === false) {
        throw new RuntimeException('Cannot reach the WebBridge text log endpoint.');
    }
    $status = $http_response_header[0] ?? '';
    if (!preg_match('~^HTTP/\S+ 200(?:\s|$)~', $status)) {
        throw new RuntimeException('WebBridge text log endpoint returned: ' . $status);
    }

    $records = [];
    $availableApps = [];
    foreach (preg_split('/\r?\n/', $content) as $line) {
        $row = parseTextLogLine($line);
        if ($row === null) continue;
        if ($row['App'] !== '') $availableApps[$row['App']] = true;
        if ($level !== '' && $row['Level'] !== $level) continue;
        if ($app !== '' && $row['App'] !== $app) continue;
        if ($source !== '' && stripos($row['Source'], $source) === false) continue;
        if ($category !== '' && stripos($row['Category'], $category) === false) continue;
        if ($event !== '' && stripos($row['Event'], $event) === false) continue;
        if ($message !== '' && stripos($row['Message'], $message) === false) continue;
        $records[] = $row;
    }

    usort($records, static function (array $a, array $b) use ($order): int {
        $cmp = strcmp($a['Timestamp'], $b['Timestamp']);
        return $order === 'desc' ? -$cmp : $cmp;
    });
    $apps = array_keys($availableApps);
    sort($apps, SORT_NATURAL | SORT_FLAG_CASE);
    return [array_slice($records, 0, $limit), $apps, [], []];
}

$rows = $apps = $serials = $sessions = [];
$error = null;
if ($requestedSource !== 'text') {
    try {
        [$rows, $apps, $serials, $sessions] = loadMysqlLogs($propertiesFile, $sessionId, $serial,
            $level, $app, $source, $category, $event, $message, $order, $limit);
        $activeSource = 'mysql';
    } catch (Throwable $ex) {
        if ($requestedSource === 'mysql') $error = $ex->getMessage();
        else $sourceNotice = 'MySQL unavailable; showing local text logs.';
    }
}
if ($activeSource !== 'mysql' && $requestedSource !== 'mysql') {
    try {
        [$rows, $apps, $serials, $sessions] = loadTextLogs($textLogUrl, $level, $app,
            $source, $category, $event, $message, $order, $limit);
        $activeSource = 'text';
    } catch (Throwable $ex) {
        $error = $ex->getMessage();
    }
}

function h(?string $value): string {
    return htmlspecialchars($value ?? '', ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
}
?>
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>SigCore Log Viewer</title>
<style>
:root {
    color-scheme: dark;
    --bg:#101317;
    --panel:#181d23;
    --panel2:#20262e;
    --border:#343c47;
    --text:#e8edf2;
    --muted:#9ca8b6;
    --green:#4caf50;
    --info:#79b8ff;
    --warn:#ffd166;
    --error:#ff6b6b;
}
* { box-sizing:border-box; }
body {
    margin:0;
    background:var(--bg);
    color:var(--text);
    font-family:Segoe UI, Arial, sans-serif;
}
header {
    padding:14px 18px;
    border-bottom:1px solid var(--border);
    background:#15191e;
    display:flex;
    justify-content:space-between;
    align-items:center;
}
h1 { margin:0; font-size:20px; font-weight:600; }
main { padding:14px; }
form {
    display:grid;
    grid-template-columns:repeat(6, minmax(130px,1fr));
    gap:10px;
    margin-bottom:12px;
}
.field label {
    display:block;
    color:var(--muted);
    font-size:12px;
    margin-bottom:4px;
}
input,select,button {
    width:100%;
    padding:8px 9px;
    border:1px solid var(--border);
    border-radius:5px;
    background:var(--panel);
    color:var(--text);
}
button {
    cursor:pointer;
    background:var(--panel2);
}
button:hover { border-color:#5b6675; }
.message-filter { grid-column:span 3; }
.actions {
    display:flex;
    gap:8px;
    margin-bottom:12px;
}
.actions button,
.actions a {
    width:auto;
    min-width:90px;
}
.actions a {
    padding:8px 12px;
    border:1px solid var(--border);
    border-radius:5px;
    background:var(--panel2);
    color:var(--text);
    text-decoration:none;
    text-align:center;
}
.notice { margin-bottom:12px; padding:10px; border:1px solid var(--border); border-radius:5px; color:var(--muted); }
.error {
    margin-bottom:12px;
    padding:10px;
    border:1px solid #7b333d;
    border-radius:5px;
    background:#351d22;
    color:#ffd7dc;
}
.table-wrap {
    overflow-x:auto;
    border:1px solid var(--border);
    border-radius:6px;
}
table {
    width:100%;
    border-collapse:collapse;
    min-width:1200px;
    font-size:13px;
}
th {
    position:sticky;
    top:0;
    z-index:2;
    padding:8px;
    text-align:left;
    background:var(--panel2);
    border-bottom:1px solid var(--border);
}
td {
    padding:7px 8px;
    border-bottom:1px solid #252b33;
    vertical-align:top;
}
tr:hover td { background:#171c22; }
.level-INFO { color:var(--info); }
.level-WARN { color:var(--warn); font-weight:700; }
.level-ERROR { color:var(--error); font-weight:700; }
.message { min-width:380px; white-space:normal; }
.count { color:var(--muted); font-size:13px; }
@media (max-width:1100px) {
    form { grid-template-columns:repeat(3,1fr); }
}
</style>
</head>
<body>
<header>
    <h1>SigCore Log Viewer</h1>
    <div class="count"><?= h(strtoupper($activeSource)) ?> · <?= count($rows) ?> record<?= count($rows) === 1 ? '' : 's' ?></div>
</header>

<main>
<?php if ($sourceNotice !== ''): ?><div class="notice"><?= h($sourceNotice) ?></div><?php endif; ?>
<?php if ($error !== null): ?>
    <div class="error"><?= h($error) ?></div>
<?php endif; ?>

<form method="get" id="filterForm">
    <div class="field">
        <label>Log source</label>
        <select name="logsource">
            <?php foreach (['auto' => 'Auto (MySQL / Text)', 'mysql' => 'MySQL', 'text' => 'Text'] as $v => $label): ?>
                <option value="<?= h($v) ?>" <?= $requestedSource === $v ? 'selected' : '' ?>><?= h($label) ?></option>
            <?php endforeach; ?>
        </select>
    </div>
    <div class="field">
        <label>Session</label>
        <select name="session">
            <option value="">All</option>
            <?php foreach ($sessions as $s): ?>
                <option value="<?= (int)$s['Id'] ?>" <?= $sessionId === (string)$s['Id'] ? 'selected' : '' ?>>
                    <?= (int)$s['Id'] ?> - <?= h($s['SerialNumber']) ?> - <?= h($s['HostName']) ?> - <?= h($s['StartedAt']) ?>
                </option>
            <?php endforeach; ?>
        </select>
    </div>

    <div class="field">
        <label>Unit / Serial Number</label>
        <select name="serial">
            <option value="">All</option>
            <?php foreach ($serials as $v): ?>
                <option value="<?= h($v) ?>" <?= $serial === $v ? 'selected' : '' ?>><?= h($v) ?></option>
            <?php endforeach; ?>
        </select>
    </div>

    <div class="field">
        <label>Level</label>
        <select name="level">
            <option value="">All</option>
            <?php foreach ($allowedLevels as $v): ?>
                <option value="<?= h($v) ?>" <?= $level === $v ? 'selected' : '' ?>><?= h($v) ?></option>
            <?php endforeach; ?>
        </select>
    </div>

    <div class="field">
        <label>App</label>
        <select name="app">
            <option value="">All</option>
            <?php foreach ($apps as $v): ?>
                <option value="<?= h($v) ?>" <?= $app === $v ? 'selected' : '' ?>><?= h($v) ?></option>
            <?php endforeach; ?>
        </select>
    </div>

    <div class="field">
        <label>Source</label>
        <input name="source" value="<?= h($source) ?>" placeholder="Log.cs">
    </div>

    <div class="field">
        <label>Category</label>
        <input name="category" value="<?= h($category) ?>" placeholder="Lifecycle">
    </div>

    <div class="field">
        <label>Event</label>
        <input name="event" value="<?= h($event) ?>" placeholder="Startup">
    </div>

    <div class="field message-filter">
        <label>Message contains</label>
        <input name="message" value="<?= h($message) ?>" placeholder="Search message text">
    </div>

    <div class="field">
        <label>Rows</label>
        <select name="limit">
            <?php foreach ($allowedLimits as $v): ?>
                <option value="<?= $v ?>" <?= $limit === $v ? 'selected' : '' ?>><?= $v ?></option>
            <?php endforeach; ?>
        </select>
    </div>

    <div class="field">
        <label>Order</label>
        <select name="order">
            <option value="desc" <?= $order === 'desc' ? 'selected' : '' ?>>Newest first</option>
            <option value="asc" <?= $order === 'asc' ? 'selected' : '' ?>>Oldest first</option>
        </select>
    </div>

    <div class="actions">
        <button type="submit">Filter</button>
        <a href="<?= h($_SERVER['PHP_SELF']) ?>">Clear</a>
    </div>
</form>

<div class="table-wrap">
<table>
<thead>
<tr>
    <th>ID</th>
    <th>Session</th>
    <th>Unit</th>
    <th>Timestamp</th>
    <th>Level</th>
    <th>App</th>
    <th>Source</th>
    <th>Category</th>
    <th>Event</th>
    <th>Message</th>
</tr>
</thead>
<tbody>
<?php foreach ($rows as $r): ?>
<tr>
    <td><?= (int)$r['Id'] ?></td>
    <td><?= (int)$r['SessionId'] ?></td>
    <td><?= h($r['SerialNumber']) ?></td>
    <td><?= h($r['Timestamp']) ?></td>
    <td class="level-<?= h($r['Level']) ?>"><?= h($r['Level']) ?></td>
    <td><?= h($r['App']) ?></td>
    <td><?= h($r['Source']) ?></td>
    <td><?= h($r['Category']) ?></td>
    <td><?= h($r['Event']) ?></td>
    <td class="message"><?= h($r['Message']) ?></td>
</tr>
<?php endforeach; ?>
</tbody>
</table>
</div>
</main>

<script>
(() => {
    const form = document.getElementById('filterForm');
    if (!form) return;

    let timer = null;

    const submitNow = () => {
        clearTimeout(timer);
        form.requestSubmit();
    };

    const submitSoon = () => {
        clearTimeout(timer);
        timer = setTimeout(() => form.requestSubmit(), 300);
    };

    form.querySelectorAll('select').forEach(el => {
        el.addEventListener('change', submitNow);
    });

    form.querySelectorAll('input[type="text"], input[type="search"], input[type="number"]').forEach(el => {
        el.addEventListener('input', submitSoon);
        el.addEventListener('change', submitNow);
    });
})();
</script>

</body>
</html>
