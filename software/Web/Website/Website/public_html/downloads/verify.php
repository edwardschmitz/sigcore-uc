<?php
session_start();

header("Cache-Control: no-store, no-cache, must-revalidate, max-age=0");
header("Pragma: no-cache");
header("Expires: 0");

date_default_timezone_set('UTC');

require __DIR__ . '/../../includes/db.php';
require __DIR__ . '/../../includes/send_verification.php';

$token = $_GET['token'] ?? '';
$email = strtolower(trim($_GET['email'] ?? ''));
$now   = gmdate('Y-m-d H:i:s');

/* ─────────────────────────────────────────────
   Helper: render styled error page
   ───────────────────────────────────────────── */
function render_invalid_link() {
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>SigCore UC Downloads</title>
    <link rel="stylesheet" href="../styles.css">
</head>
<body>

<div id="header-container"></div>
<script>
fetch("../header.html")
    .then(r => r.text())
    .then(html => document.getElementById("header-container").innerHTML = html);
</script>

<main class="main-content">
    <div class="section-title">
        <p class="large-text" style="margin-left: 15px;">Software Downloads</p>
    </div>

    <div class="specs-container">
        <div class="spec-card">
            <h1>Invalid or Expired Link</h1>
            <p style="margin-top:20px;text-align:center;color:#a00000;font-weight:bold;">
                This verification link is invalid or has expired.<br><br>
                Please return to the downloads page and request a new verification email.
            </p>
            <div style="text-align:center;margin-top:30px;">
                <a href="index.php"
                   class="download-link"
                   style="padding:12px 28px;font-size:1.1rem;">
                    Return to Downloads
                </a>
            </div>
        </div>
    </div>
</main>

<div id="footer-container"></div>
<script>
fetch("../footer.html")
    .then(r => r.text())
    .then(html => document.getElementById("footer-container").innerHTML = html);
</script>

</body>
</html>
<?php
exit;
}

/* ─────────────────────────────────────────────
   Validation
   ───────────────────────────────────────────── */
if ($token === '' || !filter_var($email, FILTER_VALIDATE_EMAIL)) {
    render_invalid_link();
}

/* ─────────────────────────────────────────────
   Look for valid verification token
   ───────────────────────────────────────────── */
$stmt = $pdo->prepare(
    "SELECT expires_at
     FROM email_verifications
     WHERE token = ?
       AND email = ?"
);
$stmt->execute([$token, $email]);
$row = $stmt->fetch(PDO::FETCH_ASSOC);

if ($row) {

    if ($row['expires_at'] <= $now) {
        render_invalid_link();
    }

    // Mark user verified
    $pdo->prepare(
        "UPDATE users
         SET verified = 1,
             verified_at = ?
         WHERE email = ?"
    )->execute([$now, $email]);

    // Delete verification token
    $pdo->prepare(
        "DELETE FROM email_verifications
         WHERE email = ?"
    )->execute([$email]);

    // Notify admin (non-blocking)
    try {
        notifyAdminOfVerification($email);
    } catch (Exception $e) {
        // Optional: log error
    }

    $_SESSION['verified_email'] = $email;
    header("Location: files.php");
    exit;
}

/* ─────────────────────────────────────────────
   Fallback: already verified user
   ───────────────────────────────────────────── */
$stmt = $pdo->prepare(
    "SELECT verified FROM users WHERE email = ?"
);
$stmt->execute([$email]);
$user = $stmt->fetch(PDO::FETCH_ASSOC);

if ($user && (int)$user['verified'] === 1) {
    $_SESSION['verified_email'] = $email;
    header("Location: files.php");
    exit;
}

render_invalid_link();
