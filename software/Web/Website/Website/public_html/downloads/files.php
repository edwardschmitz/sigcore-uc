<?php
session_start();

header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
header('Pragma: no-cache');
header('Expires: 0');

require __DIR__ . '/../../includes/db.php';

function requestHeaderValue($name)
{
    $serverName = 'HTTP_' . strtoupper(
        str_replace('-', '_', $name)
    );

    return isset($_SERVER[$serverName])
        ? $_SERVER[$serverName]
        : null;
}

function logOldEndpointAttempt($pdo, $event)
{
    try {
        $traceId = md5(
            uniqid((string) mt_rand(), true)
        );

        $method = isset($_SERVER['REQUEST_METHOD'])
            ? $_SERVER['REQUEST_METHOD']
            : '';

        $requestUri = isset($_SERVER['REQUEST_URI'])
            ? $_SERVER['REQUEST_URI']
            : '';

        $ipAddress = isset($_SERVER['REMOTE_ADDR'])
            ? $_SERVER['REMOTE_ADDR']
            : 'unknown';

        $email = isset($_POST['email'])
            ? strtolower(trim($_POST['email']))
            : null;

        if ($email === '') {
            $email = null;
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
            ':user_agent' => isset($_SERVER['HTTP_USER_AGENT'])
                ? $_SERVER['HTTP_USER_AGENT']
                : null,
            ':referer' => isset($_SERVER['HTTP_REFERER'])
                ? $_SERVER['HTTP_REFERER']
                : null,
            ':accept_language' => isset($_SERVER['HTTP_ACCEPT_LANGUAGE'])
                ? $_SERVER['HTTP_ACCEPT_LANGUAGE']
                : null,
            ':accept_encoding' => isset($_SERVER['HTTP_ACCEPT_ENCODING'])
                ? $_SERVER['HTTP_ACCEPT_ENCODING']
                : null,
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
            'Old endpoint logging failed: ' . $e->getMessage()
        );
    }
}

logOldEndpointAttempt(
    $pdo,
    'old_file_attempt'
);

http_response_code(410);
header('Content-Type: text/plain; charset=UTF-8');

exit('Gone');