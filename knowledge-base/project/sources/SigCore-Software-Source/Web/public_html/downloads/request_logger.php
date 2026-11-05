<?php

function getRequestHeadersForLogging()
{
    if (function_exists('getallheaders')) {
        $headers = getallheaders();

        if (is_array($headers)) {
            return $headers;
        }
    }

    $headers = array();

    foreach ($_SERVER as $key => $value) {
        if (strpos($key, 'HTTP_') === 0) {
            $headerName = substr($key, 5);
            $headerName = str_replace('_', '-', strtolower($headerName));
            $headers[$headerName] = $value;
        }
    }

    return $headers;
}

function getRequestHeaderValue($headers, $requestedName)
{
    foreach ($headers as $name => $value) {
        if (strcasecmp((string)$name, $requestedName) === 0) {
            return is_string($value) ? $value : null;
        }
    }

    return null;
}

function createRequestTraceId()
{
    return bin2hex(random_bytes(16));
}

function isValidRequestTraceId($traceId)
{
    return is_string($traceId)
        && preg_match('/^[a-f0-9]{32}$/', $traceId) === 1;
}

function getOrCreateRequestTraceId()
{
    $existingTraceId = isset($_COOKIE['sigcore_trace_id'])
        ? $_COOKIE['sigcore_trace_id']
        : null;

    if (isValidRequestTraceId($existingTraceId)) {
        return $existingTraceId;
    }

    $traceId = createRequestTraceId();

    $isSecure =
        isset($_SERVER['HTTPS'])
        && $_SERVER['HTTPS'] !== ''
        && strtolower((string)$_SERVER['HTTPS']) !== 'off';

    /*
     * Older PHP-compatible setcookie format.
     */
    setcookie(
        'sigcore_trace_id',
        $traceId,
        time() + 86400,
        '/downloads/',
        '',
        $isSecure,
        true
    );

    return $traceId;
}

function getRequestIpAddress()
{
    $ipAddress = isset($_SERVER['REMOTE_ADDR'])
        ? $_SERVER['REMOTE_ADDR']
        : '';

    if (!is_string($ipAddress) || $ipAddress === '') {
        return 'unknown';
    }

    return substr($ipAddress, 0, 45);
}

function logRequestEvent(
    $pdo,
    $event,
    $traceId,
    $email = null
) {
    try {
        $allowedEvents = array(
            'page_get',
            'form_post',
            'javascript'
        );

        if (!in_array($event, $allowedEvents, true)) {
            throw new InvalidArgumentException('Invalid log event.');
        }

        if (!isValidRequestTraceId($traceId)) {
            $traceId = createRequestTraceId();
        }

        if ($email !== null) {
            $email = strtolower(trim($email));

            if ($email === '') {
                $email = null;
            } else {
                $email = substr($email, 0, 255);
            }
        }

        $headers = getRequestHeadersForLogging();

        $method = isset($_SERVER['REQUEST_METHOD'])
            ? $_SERVER['REQUEST_METHOD']
            : '';

        $requestUri = isset($_SERVER['REQUEST_URI'])
            ? $_SERVER['REQUEST_URI']
            : '';

        $userAgent = isset($_SERVER['HTTP_USER_AGENT'])
            ? $_SERVER['HTTP_USER_AGENT']
            : getRequestHeaderValue($headers, 'User-Agent');

        $referer = isset($_SERVER['HTTP_REFERER'])
            ? $_SERVER['HTTP_REFERER']
            : getRequestHeaderValue($headers, 'Referer');

        $acceptLanguage = isset($_SERVER['HTTP_ACCEPT_LANGUAGE'])
            ? $_SERVER['HTTP_ACCEPT_LANGUAGE']
            : getRequestHeaderValue($headers, 'Accept-Language');

        $acceptEncoding = isset($_SERVER['HTTP_ACCEPT_ENCODING'])
            ? $_SERVER['HTTP_ACCEPT_ENCODING']
            : getRequestHeaderValue($headers, 'Accept-Encoding');

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

        $statement = $pdo->prepare($sql);

        return $statement->execute(array(
            ':trace_id' => $traceId,
            ':event' => $event,
            ':ip_address' => getRequestIpAddress(),
            ':method' => substr((string)$method, 0, 8),
            ':request_uri' => substr((string)$requestUri, 0, 255),
            ':email' => $email,
            ':user_agent' => $userAgent,
            ':referer' => $referer,
            ':accept_language' => $acceptLanguage,
            ':accept_encoding' => $acceptEncoding,

            ':sec_ch_ua' =>
                getRequestHeaderValue($headers, 'Sec-CH-UA'),

            ':sec_ch_ua_mobile' =>
                getRequestHeaderValue($headers, 'Sec-CH-UA-Mobile'),

            ':sec_ch_ua_platform' =>
                getRequestHeaderValue($headers, 'Sec-CH-UA-Platform'),

            ':sec_ch_ua_platform_version' =>
                getRequestHeaderValue(
                    $headers,
                    'Sec-CH-UA-Platform-Version'
                ),

            ':sec_ch_ua_arch' =>
                getRequestHeaderValue($headers, 'Sec-CH-UA-Arch'),

            ':sec_ch_ua_bitness' =>
                getRequestHeaderValue($headers, 'Sec-CH-UA-Bitness'),

            ':sec_ch_ua_model' =>
                getRequestHeaderValue($headers, 'Sec-CH-UA-Model')
        ));

    } catch (Exception $exception) {
        error_log(
            'Request logging failed: ' . $exception->getMessage()
        );

        return false;
    }
}