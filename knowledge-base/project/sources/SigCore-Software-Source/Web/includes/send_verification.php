<?php

use PHPMailer\PHPMailer\PHPMailer;
use PHPMailer\PHPMailer\Exception;

require __DIR__ . '/secrets.php';
require __DIR__ . '/db.php';
require __DIR__ . '/phpmailer/PHPMailer.php';
require __DIR__ . '/phpmailer/SMTP.php';
require __DIR__ . '/phpmailer/Exception.php';

function sendVerificationEmail(string $email): void
{
    global $pdo;

    // Force UTC to match verify.php
    date_default_timezone_set('UTC');

    // Generate token ONCE
    $token = bin2hex(random_bytes(32));
    $expires = gmdate('Y-m-d H:i:s', time() + 3600);

    // Remove any existing tokens for this email
    $pdo->prepare(
        "DELETE FROM email_verifications WHERE email = ?"
    )->execute([$email]);

    // Insert new token
    $stmt = $pdo->prepare(
        "INSERT INTO email_verifications (email, token, expires_at)
         VALUES (?, ?, ?)"
    );
    $stmt->execute([$email, $token, $expires]);

    // Optional: log token once during debugging
    // error_log("Verification token for $email: $token");

    $link = "https://sigcoreuc.com/downloads/verify-.php?token=$token&email=" . urlencode($email);

    $mail = new PHPMailer(true);

    // SMTP config
    $mail->isSMTP();
    $mail->Host = SMTP_HOST;
    $mail->SMTPAuth = true;
    $mail->Username = SMTP_USER;
    $mail->Password = SMTP_PASS;
    $mail->SMTPSecure = PHPMailer::ENCRYPTION_STARTTLS;
    $mail->Port = SMTP_PORT;

    $mail->setFrom(SMTP_USER, 'SigCore UC');
    $mail->addAddress($email);

    $mail->Subject = 'Verify your email';
    $mail->Body =
        "You requested access to SigCore UC downloads.\n\n" .
        "Verify your email by clicking this link:\n$link\n\n" .
        "This link expires in 1 hour.\n\n" .
        "If you did not request this, ignore this email.";

    $mail->send();
}
function notifyAdminOfVerification(string $email): void
{
    date_default_timezone_set('UTC');
    $now = gmdate('Y-m-d H:i:s');

    $mail = new PHPMailer(true);

    $mail->isSMTP();
    $mail->Host = SMTP_HOST;
    $mail->SMTPAuth = true;
    $mail->Username = SMTP_USER;
    $mail->Password = SMTP_PASS;
    $mail->SMTPSecure = PHPMailer::ENCRYPTION_STARTTLS;
    $mail->Port = SMTP_PORT;

    $mail->setFrom(SMTP_USER, 'SigCore UC');
    $mail->addAddress('eddie.schmitz@hotmail.com');

    $mail->Subject = 'New Verified Download User';

    $mail->Body =
        "A new user has verified their email.\n\n" .
        "Email: $email\n" .
        "Time (UTC): $now\n" .
        "IP: " . ($_SERVER['REMOTE_ADDR'] ?? 'Unknown') . "\n" .
        "User Agent: " . ($_SERVER['HTTP_USER_AGENT'] ?? 'Unknown');

    $mail->send();
}
