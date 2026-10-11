<?php
session_start();

// Disable caching
header("Cache-Control: no-store, no-cache, must-revalidate, max-age=0");
header("Pragma: no-cache");
header("Expires: 0");

require __DIR__ . '/../../includes/db.php';

$message = '';

/*
 * Create one trace ID for this visit.
 */
if (empty($_SESSION['download_trace_id'])) {
    $_SESSION['download_trace_id'] = md5(
        uniqid((string) mt_rand(), true)
    );
}

$traceId = $_SESSION['download_trace_id'];


/*
 * Read an HTTP header safely.
 */
function requestHeaderValue($name)
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
 * Write one event to request_log.
 *
 * Any logging error is caught so it cannot break the download page.
 */
function writeRequestLog($pdo, $event, $traceId, $email = null)
{
    try {
        $method = isset($_SERVER['REQUEST_METHOD'])
            ? $_SERVER['REQUEST_METHOD']
            : '';

        $requestUri = isset($_SERVER['REQUEST_URI'])
            ? $_SERVER['REQUEST_URI']
            : '';

        $ipAddress = isset($_SERVER['REMOTE_ADDR'])
            ? $_SERVER['REMOTE_ADDR']
            : 'unknown';

        $userAgent = isset($_SERVER['HTTP_USER_AGENT'])
            ? $_SERVER['HTTP_USER_AGENT']
            : null;

        $referer = isset($_SERVER['HTTP_REFERER'])
            ? $_SERVER['HTTP_REFERER']
            : null;

        $acceptLanguage = isset($_SERVER['HTTP_ACCEPT_LANGUAGE'])
            ? $_SERVER['HTTP_ACCEPT_LANGUAGE']
            : null;

        $acceptEncoding = isset($_SERVER['HTTP_ACCEPT_ENCODING'])
            ? $_SERVER['HTTP_ACCEPT_ENCODING']
            : null;

        if ($email !== null) {
            $email = strtolower(trim($email));

            if ($email === '') {
                $email = null;
            }
        }

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
            ':event' => $event,
            ':ip_address' => substr($ipAddress, 0, 45),
            ':method' => substr($method, 0, 8),
            ':request_uri' => substr($requestUri, 0, 255),
            ':email' => $email,
            ':user_agent' => $userAgent,
            ':referer' => $referer,
            ':accept_language' => $acceptLanguage,
            ':accept_encoding' => $acceptEncoding,
            ':sec_ch_ua' => requestHeaderValue('Sec-CH-UA'),
            ':sec_ch_ua_mobile' =>
                requestHeaderValue('Sec-CH-UA-Mobile'),
            ':sec_ch_ua_platform' =>
                requestHeaderValue('Sec-CH-UA-Platform'),
            ':sec_ch_ua_platform_version' =>
                requestHeaderValue('Sec-CH-UA-Platform-Version'),
            ':sec_ch_ua_arch' =>
                requestHeaderValue('Sec-CH-UA-Arch'),
            ':sec_ch_ua_bitness' =>
                requestHeaderValue('Sec-CH-UA-Bitness'),
            ':sec_ch_ua_model' =>
                requestHeaderValue('Sec-CH-UA-Model')
        ));

    } catch (Exception $e) {
        error_log(
            'Request logging failed: ' . $e->getMessage()
        );
    }
}


/*
 * Handle the JavaScript callback before normal form processing.
 */
if (
    $_SERVER['REQUEST_METHOD'] === 'POST'
    && isset($_POST['log_event'])
    && $_POST['log_event'] === 'javascript'
) {
    $postedTraceId = isset($_POST['trace_id'])
        ? $_POST['trace_id']
        : $traceId;

    writeRequestLog(
        $pdo,
        'javascript',
        $postedTraceId
    );

    http_response_code(204);
    exit;
}


/*
 * Handle normal form submission.
 */
if ($_SERVER['REQUEST_METHOD'] === 'POST') {

    $postedTraceId = isset($_POST['trace_id'])
        ? $_POST['trace_id']
        : $traceId;

    $email = strtolower(
        trim(
            isset($_POST['email'])
                ? $_POST['email']
                : ''
        )
    );

    writeRequestLog(
        $pdo,
        'form_post',
        $postedTraceId,
        $email
    );

    require __DIR__ . '/../../includes/send_verification.php';

    if (filter_var($email, FILTER_VALIDATE_EMAIL)) {

        $stmt = $pdo->prepare(
            "SELECT verified FROM users WHERE email = ?"
        );

        $stmt->execute(array($email));

        $user = $stmt->fetch(PDO::FETCH_ASSOC);

        if ($user && $user['verified']) {
            $_SESSION['verified_email'] = $email;

            echo "<script>
                localStorage.setItem(
                    'sigcore_download_email',
                    " . json_encode($email) . "
                );
                location.href = 'files-.php';
            </script>";

            exit;
        }

        $stmt = $pdo->prepare(
            "INSERT IGNORE INTO users (email) VALUES (?)"
        );

        $stmt->execute(array($email));

        sendVerificationEmail($email);

        $message = 'Check your email to verify your address.';

    } else {
        $message = 'Please enter a valid email address.';
    }

} else {

    writeRequestLog(
        $pdo,
        'page_get',
        $traceId
    );
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">

    <meta
        http-equiv="X-UA-Compatible"
        content="IE=edge"
    >

    <meta
        name="viewport"
        content="width=device-width, initial-scale=1.0"
    >

    <title>SigCore UC Downloads</title>

    <link rel="stylesheet" href="../styles.css">

    <script
        defer
        src="https://cloud.umami.is/script.js"
        data-website-id="24ed1392-77e8-4294-876a-e55dc3d9f56e"
    ></script>
</head>

<body>

<div id="header-container"></div>

<script>
    fetch("../header.html")
        .then(function (response) {
            return response.text();
        })
        .then(function (html) {
            document.getElementById(
                "header-container"
            ).innerHTML = html;
        });
</script>

<main class="main-content">

    <div class="section-title">
        <p
            class="large-text"
            style="margin-left: 15px;"
        >
            Software Downloads
        </p>
    </div>

    <div class="specs-container">
        <div class="spec-card">

            <h1>Email Verification</h1>

            <p>
                Enter your email address to access SigCore UC software
                downloads. A verification link will be sent to your inbox.
            </p>

            <div
                id="download-form"
                style="margin-top: 20px;"
            >
                <label for="email-decoy">
                    <strong>Email address</strong>
                </label>

                <br>

                <input
                    type="email"
                    id="email-decoy"
                    autocomplete="email"
                    style="
                        margin-top: 8px;
                        padding: 8px;
                        width: 100%;
                        max-width: 360px;
                    "
                >

                <br><br>

                <button
                    type="button"
                    class="download-link"
                    style="
                        display: inline-block;
                        padding: 12px 28px;
                        font-size: 1.1rem;
                        margin-top: 10px;
                    "
                >
                    Continue
                </button>
            </div>

            <?php if ($message !== '') { ?>
                <p
                    style="
                        margin-top: 20px;
                        text-align: center;
                        color: #a00000;
                        font-weight: bold;
                    "
                >
                    <?php
                        echo htmlspecialchars(
                            $message,
                            ENT_QUOTES,
                            'UTF-8'
                        );
                    ?>
                </p>
            <?php } ?>

        </div>
    </div>
</main>

<div id="footer-container"></div>

<script>
    fetch("../footer.html")
        .then(function (response) {
            return response.text();
        })
        .then(function (html) {
            document.getElementById(
                "footer-container"
            ).innerHTML = html;
        });
</script>

<script>
    (function () {
        var container = document.getElementById(
            'download-form'
        );

        if (!container) {
            return;
        }

        container.innerHTML = `
            <form
                method="post"
                action=""
            >
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
                    <strong>Email address</strong>
                </label>

                <br>

                <input
                    type="email"
                    name="email"
                    id="email"
                    required
                    autocomplete="email"
                    style="
                        margin-top: 8px;
                        padding: 8px;
                        width: 100%;
                        max-width: 360px;
                    "
                >

                <br><br>

                <button
                    type="submit"
                    class="download-link"
                    style="
                        display: inline-block;
                        padding: 12px 28px;
                        font-size: 1.1rem;
                        margin-top: 10px;
                    "
                >
                    Continue
                </button>
            </form>
        `;

        var savedEmail = localStorage.getItem(
            'sigcore_download_email'
        );

        if (savedEmail) {
            var emailInput = document.getElementById(
                'email'
            );

            if (emailInput) {
                emailInput.value = savedEmail;
            }
        }
    })();

    function toggleMobileMenu() {
        var menu = document.querySelector(".nav-menu");

        if (menu) {
            menu.classList.toggle("show");
        }
    }
</script>

<script>
    /*
     * This records whether JavaScript actually executed.
     */
    (function () {
        var data = new URLSearchParams();

        data.set('log_event', 'javascript');

        data.set(
            'trace_id',
            <?php echo json_encode($traceId); ?>
        );

        fetch('/downloads/index.php', {
            method: 'POST',
            body: data,
            credentials: 'same-origin',
            keepalive: true
        }).catch(function () {
            // Do nothing if logging fails.
        });
    })();
</script>

</body>
</html>