<?php
session_start();

if (!isset($_SESSION['verified_email'])) {
    header('Location: index-.php');
    exit;
}
?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>SigCore Software Downloads</title>

    <!-- styles.css is one level UP from /downloads -->
    <link rel="stylesheet" href="../styles.css">
<script defer src="https://cloud.umami.is/script.js" data-website-id="24ed1392-77e8-4294-876a-e55dc3d9f56e"></script></head>
</head>
<body>
    <div id="header-container"></div>

    <script>
        fetch("../header.html")
            .then(response => response.text())
            .then(data => document.getElementById("header-container").innerHTML = data)
            .catch(error => console.error("Error loading the header:", error));
    </script>

    <main class="main-content">
        <div class="section-title">
            <p class="large-text" style="margin-left: 15px;">Software</p>
        </div>

        <!-- Control Panel Application Section -->
        <div class="specs-container">
            <div class="spec-card">
                <h1>Control Panel Application</h1>

                <h2>1. Requirements</h2>
                <p>Works on Windows 10 or later with .NET Framework 4.8 installed.</p>

                <h2>2. Download the Installer</h2>
                <p>
                    Click below to download the latest installer for the Control Panel application:
                </p>
                <p>
                    <a class="download-link" href="ControlPanelSetup.exe">
                        Download ControlPanelSetup.exe
                    </a>
                </p>

                <h2>3. Installation Path</h2>
                <pre><code>C:\Program Files (x86)\SigCore\ControlPanel</code></pre>

                <h2>4. Features</h2>
                <ul>
                    <li>Relay control (R1–R8)</li>
                    <li>Digital and analog input monitoring</li>
                    <li>Analog output configuration</li>
                    <li>Custom calibration and settings</li>
                </ul>

                <h2>Need Help?</h2>
                <p>Visit our <a href="../support.html">Support Page</a> or <a href="../contact.html">Contact Us</a> for assistance.</p>
            </div>

            <!-- MySQL Logger Database Section -->
            <div class="spec-card">
                <h1>MySQL Logger Database</h1>
                <p>
                    Use this SQL script to create the <code>sigcore_data_logger</code>
                    database and its tables on your MySQL server. The database is
                    created empty, without sample or previously recorded data.
                </p>
                <p>
                    <a class="download-link" href="sigcore_data_logger.zip" download>
                        Download sigcore_data_logger.zip
                    </a>
                </p>
                <p>
                    To install, connect to your MySQL server using MySQL Workbench,
                    extract the SQL file from the downloaded ZIP, open it, and execute the script. Your MySQL
                    account must have permission to create databases and tables.
                    Configure the logger's database connection settings separately.
                </p>
                <p>
                    <strong>Important:</strong> This script is intended for a new
                    installation. It does not overwrite existing logger tables.
                </p>
            </div>
        </div>
    </main>

    <div id="footer-container"></div>

    <script>
        fetch("../footer.html")
            .then(response => response.text())
            .then(data => document.getElementById("footer-container").innerHTML = data)
            .catch(error => console.error("Error loading the footer:", error));
    </script>

    <script>
        function toggleMobileMenu() {
            const menu = document.querySelector(".nav-menu");
            if (menu) menu.classList.toggle("show");
        }
    </script>
</body>
</html>
