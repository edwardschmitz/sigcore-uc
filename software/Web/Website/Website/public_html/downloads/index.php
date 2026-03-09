<?php
session_start();

// Disable caching
header("Cache-Control: no-store, no-cache, must-revalidate, max-age=0");
header("Pragma: no-cache");
header("Expires: 0");

require __DIR__ . '/../../includes/db.php';

$message = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {

    require __DIR__ . '/../../includes/send_verification.php';

    $email = strtolower(trim($_POST['email'] ?? ''));

    if (filter_var($email, FILTER_VALIDATE_EMAIL)) {

        $stmt = $pdo->prepare(
            "SELECT verified FROM users WHERE email = ?"
        );
        $stmt->execute([$email]);
        $user = $stmt->fetch(PDO::FETCH_ASSOC);

        if ($user && $user['verified']) {
            $_SESSION['verified_email'] = $email;
            echo "<script>
                localStorage.setItem('sigcore_download_email', " . json_encode($email) . ");
                location.href = 'files.php';
            </script>";
            exit;
        }

        $pdo->prepare(
            "INSERT IGNORE INTO users (email) VALUES (?)"
        )->execute([$email]);

        sendVerificationEmail($email);
        $message = 'Check your email to verify your address.';
    }
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>SigCore UC Downloads</title>

    <!-- shared site styles -->
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

            <h1>Email Verification</h1>

            <p>
                Enter your email address to access SigCore UC software downloads.
                A verification link will be sent to your inbox.
            </p>

            <form method="post" style="margin-top: 20px;">
                <label for="email"><strong>Email address</strong></label><br>
                <input
                    type="email"
                    name="email"
                    id="email"
                    required
                    style="margin-top: 8px; padding: 8px; width: 100%; max-width: 360px;"
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

            <?php if ($message): ?>
                <p style="
                    margin-top: 20px;
                    text-align: center;
                    color: #a00000;
                    font-weight: bold;
                ">
                    <?= htmlspecialchars($message) ?>
                </p>
            <?php endif; ?>

        </div>
    </div>
</main>

<div id="footer-container"></div>

<script>
    fetch("../footer.html")
        .then(r => r.text())
        .then(html => document.getElementById("footer-container").innerHTML = html);
</script>

<script>
    const saved = localStorage.getItem('sigcore_download_email');
    if (saved) {
        const el = document.getElementById('email');
        if (el) el.value = saved;
    }

    function toggleMobileMenu() {
        const menu = document.querySelector(".nav-menu");
        if (menu) menu.classList.toggle("show");
    }
</script>

</body>
</html>
