<?php

session_start();

require __DIR__ . '/../../includes/db.php';

/*
 * Create one trace ID for this browser session.
 */
if (!isset($_SESSION['logger_trace_id'])) {
    $_SESSION['logger_trace_id'] = md5(
        uniqid((string)mt_rand(), true)
    );
}

$traceId = $_SESSION['logger_trace_id'];


/*
 * Read a request header.
 */
function getHeaderValue($name)
{
    $serverName = 'HTTP_' . strtoupper(
        str_replace('-', '_', $name)
    );

    if (isset($_SERVER[$serverName])) {
        return $_SERVER[$serverName];
    }

    return null;
}


/*
 * Write an event to the database.
 */
function writeLog($pdo, $event, $traceId, $email)
{
    try {
        $sql = "
            INSERT INTO request_log
            (
                timestamp_utc,
                trace_id,
                event,
                ip_address,
                method,
                request_uri,
                email,
                user_agent,
                referer,
                accept_language,
                accept_encoding,
                sec_ch_ua,
                sec_ch_ua_mobile,
                sec_ch_ua_platform,
                sec_ch_ua_platform_version,
                sec_ch_ua_arch,
                sec_ch_ua_bitness,
                sec_ch_ua_model
            )
            VALUES
            (
                UTC_TIMESTAMP(6),
                :trace_id,
                :event,
                :ip_address,
                :method,
                :request_uri,
                :email,
                :user_agent,
                :referer,
                :accept_language,
                :accept_encoding,
                :sec_ch_ua,
                :sec_ch_ua_mobile,
                :sec_ch_ua_platform,
                :sec_ch_ua_platform_version,
                :sec_ch_ua_arch,
                :sec_ch_ua_bitness,
                :sec_ch_ua_model
            )
        ";

        $stmt = $pdo->prepare($sql);

        $stmt->execute(array(
            ':trace_id' => substr($traceId, 0, 32),

            ':event' => substr($event, 0, 32),

            ':ip_address' => isset($_SERVER['REMOTE_ADDR'])
                ? substr($_SERVER['REMOTE_ADDR'], 0, 45)
                : 'unknown',

            ':method' => isset($_SERVER['REQUEST_METHOD'])
                ? substr($_SERVER['REQUEST_METHOD'], 0, 8)
                : '',

            ':request_uri' => isset($_SERVER['REQUEST_URI'])
                ? substr($_SERVER['REQUEST_URI'], 0, 255)
                : '',

            ':email' => $email,

            ':user_agent' => isset($_SERVER['HTTP_USER_AGENT'])
                ? $_SERVER['HTTP_USER_AGENT']
                : null,

            ':referer' => isset($_SERVER['HTTP_REFERER'])
                ? $_SERVER['HTTP_REFERER']
                : null,

            ':accept_language' =>
                getHeaderValue('Accept-Language'),

            ':accept_encoding' =>
                getHeaderValue('Accept-Encoding'),

            ':sec_ch_ua' =>
                getHeaderValue('Sec-CH-UA'),

            ':sec_ch_ua_mobile' =>
                getHeaderValue('Sec-CH-UA-Mobile'),

            ':sec_ch_ua_platform' =>
                getHeaderValue('Sec-CH-UA-Platform'),

            ':sec_ch_ua_platform_version' =>
                getHeaderValue('Sec-CH-UA-Platform-Version'),

            ':sec_ch_ua_arch' =>
                getHeaderValue('Sec-CH-UA-Arch'),

            ':sec_ch_ua_bitness' =>
                getHeaderValue('Sec-CH-UA-Bitness'),

            ':sec_ch_ua_model' =>
                getHeaderValue('Sec-CH-UA-Model')
        ));

        return true;

    } catch (Exception $e) {
        return $e->getMessage();
    }
}


/*
 * JavaScript callback.
 */
if (
    isset($_SERVER['REQUEST_METHOD'])
    && $_SERVER['REQUEST_METHOD'] === 'POST'
    && isset($_POST['log_event'])
    && $_POST['log_event'] === 'javascript'
) {
    $postedTraceId = isset($_POST['trace_id'])
        ? $_POST['trace_id']
        : $traceId;

    writeLog(
        $pdo,
        'javascript',
        $postedTraceId,
        null
    );

    http_response_code(204);
    exit;
}


/*
 * Normal test form submission.
 */
if (
    isset($_SERVER['REQUEST_METHOD'])
    && $_SERVER['REQUEST_METHOD'] === 'POST'
) {
    $email = isset($_POST['email'])
        ? strtolower(trim($_POST['email']))
        : null;

    $postedTraceId = isset($_POST['trace_id'])
        ? $_POST['trace_id']
        : $traceId;

    $result = writeLog(
        $pdo,
        'form_post',
        $postedTraceId,
        $email
    );

    if ($result === true) {
        $statusMessage = 'Form submission logged successfully.';
    } else {
        $statusMessage = 'Logging failed: ' . $result;
    }

} else {

    /*
     * Ordinary page load.
     */
    $result = writeLog(
        $pdo,
        'page_get',
        $traceId,
        null
    );

    if ($result === true) {
        $statusMessage = 'Page load logged successfully.';
    } else {
        $statusMessage = 'Logging failed: ' . $result;
    }
}

?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Request Logger Test</title>
</head>

<body>

<h1>Request Logger Test</h1>

<p>
    <?php
        echo htmlspecialchars(
            $statusMessage,
            ENT_QUOTES,
            'UTF-8'
        );
    ?>
</p>

<p>
    Trace ID:
    <strong>
        <?php
            echo htmlspecialchars(
                $traceId,
                ENT_QUOTES,
                'UTF-8'
            );
        ?>
    </strong>
</p>

<form method="post" action="">

    <input
        type="hidden"
        name="trace_id"
        value="<?php
            echo htmlspecialchars(
                $traceId,
                ENT_QUOTES,
                'UTF-8'
            );
        ?>"
    >

    <label for="email">
        Test email address:
    </label>

    <input
        type="email"
        id="email"
        name="email"
        required
    >

    <button type="submit">
        Test Form Submission
    </button>

</form>

<script>
(function () {
    var data = new URLSearchParams();

    data.set('log_event', 'javascript');

    data.set(
        'trace_id',
        <?php echo json_encode($traceId); ?>
    );

    fetch('test_logger.php', {
        method: 'POST',
        body: data,
        credentials: 'same-origin',
        keepalive: true
    });
})();
</script>

</body>
</html>