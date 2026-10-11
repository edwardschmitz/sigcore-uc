import argparse
import json
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request

try:
    from zeroconf import ServiceBrowser, ServiceListener, Zeroconf
except ImportError:
    print("ERROR: Python package 'zeroconf' is required.")
    print("Install it with:")
    print("    python -m pip install zeroconf")
    sys.exit(2)

try:
    import mysql.connector
except ImportError:
    print("ERROR: Python package 'mysql-connector-python' is required.")
    print("Install it with:")
    print("    python -m pip install mysql-connector-python")
    sys.exit(2)


SERVICE_TYPE = "_sigcore._tcp.local."
DISCOVERY_SECONDS = 3

CORE_PORT = 7020
WEB_PORT = 80
STATUS_PATH = "/status.html"
EXPECTED_STATUS_TEXT = "SigCore UC Status and Live Update"

SSH_USER = "sigcoreuc"
PROPERTIES_PATH = "/opt/sigcore/properties.json"
PROPERTIES_BACKUP_PATH = "/opt/sigcore/properties.json.regression-backup"
FALLBACK_LOG_PATH = "/tmp/sigcore-log/log.txt"

DB_HOST = "192.168.0.125"
DB_PORT = 3306
DB_NAME = "sigcore_data_logger"
DB_USER = "edward"
DB_PASSWORD = "eddedd"

GOOD_LOG_WAIT_SECONDS = 25
BAD_LOG_WAIT_SECONDS = 35
BAD_ADDRESS_WAIT_SECONDS = 45
BAD_DB_HOST = "192.168.0.254"
REBOOT_TIMEOUT_SECONDS = 150

EXPECTED_APPS = {
    "SigCoreLauncher",
    "SigCoreServer",
    "SigCoreUpdater",
    "WebBridge",
}


class SigCoreListener(ServiceListener):
    def __init__(self):
        self.names = set()

    def add_service(self, zeroconf, service_type, name):
        self.names.add(name)

    def update_service(self, zeroconf, service_type, name):
        self.names.add(name)

    def remove_service(self, zeroconf, service_type, name):
        pass


def run_test(name, details, function):
    print(f"TEST  {name}")
    for line in details:
        print(f"      {line}")

    try:
        message = function()
        if message:
            print(f"      {message}")
        print(f"PASS  {name}")
        print()
        return True
    except Exception as e:
        print(f"      Actual: {e}")
        print(f"FAIL  {name}")
        print()
        return False


def discover_sigcore_units():
    zeroconf = Zeroconf()
    listener = SigCoreListener()
    ServiceBrowser(zeroconf, SERVICE_TYPE, listener)

    print("SigCore Regression Test")
    print("=======================")
    print()
    print("DISCOVERY")
    print(f"      Service: {SERVICE_TYPE}")
    print(f"      Search time: {DISCOVERY_SECONDS} seconds")
    print()

    time.sleep(DISCOVERY_SECONDS)

    units = []

    for service_name in sorted(listener.names):
        info = zeroconf.get_service_info(
            SERVICE_TYPE,
            service_name,
            timeout=2000,
        )

        if not info:
            continue

        addresses = info.parsed_addresses()
        ip = addresses[0] if addresses else None
        hostname = info.server.rstrip(".") if info.server else None

        units.append({
            "service_name": service_name,
            "hostname": hostname,
            "ip": ip,
            "advertised_port": info.port,
        })

    zeroconf.close()
    return units


def hostname_test(hostname):
    def run():
        ip = socket.gethostbyname(hostname)
        if not ip:
            raise Exception("Hostname did not resolve")
        return f"Actual: resolved to {ip}"
    return run


def tcp_test(host, port):
    def run():
        with socket.create_connection((host, port), timeout=3):
            pass
        return "Actual: TCP connection succeeded"
    return run


def status_page_test(host):
    def run():
        url = f"http://{host}{STATUS_PATH}"
        try:
            with urllib.request.urlopen(url, timeout=5) as response:
                status = response.status
                body = response.read().decode("utf-8", errors="replace")

                if status != 200:
                    raise Exception(f"HTTP {status}")

                if EXPECTED_STATUS_TEXT not in body:
                    raise Exception(
                        f"HTTP 200, but expected text was not found: "
                        f"{EXPECTED_STATUS_TEXT!r}"
                    )

                return (
                    f"Actual: HTTP {status}; "
                    f"found expected text {EXPECTED_STATUS_TEXT!r}"
                )

        except urllib.error.HTTPError as e:
            raise Exception(f"HTTP {e.code} {e.reason}")

    return run


def test_unit(unit):
    hostname = unit["hostname"]
    ip = unit["ip"]
    advertised_port = unit["advertised_port"]
    display_name = hostname or ip or unit["service_name"]

    print()
    print("--------------------------------------------------")
    print(f"UNIT  {display_name}")
    print("--------------------------------------------------")
    print(f"      Service: {unit['service_name']}")
    print(f"      Hostname: {hostname or 'not provided'}")
    print(f"      IP: {ip or 'not provided'}")
    print(f"      Advertised service port: {advertised_port}")
    print()

    results = []

    if hostname:
        results.append(
            run_test(
                "Hostname resolution",
                [
                    f"Host: {hostname}",
                    "Expected: hostname resolves to an IP address",
                ],
                hostname_test(hostname),
            )
        )
        test_host = hostname
    elif ip:
        print("SKIP  Hostname resolution")
        print("      Reason: discovery did not provide a hostname.")
        print()
        test_host = ip
    else:
        print("FAIL  Unit has no usable hostname or IP address")
        print()
        return [False]

    results.append(
        run_test(
            "Core TCP connection",
            [
                f"Host: {test_host}",
                f"Port: {CORE_PORT}",
                "Expected: TCP connection succeeds",
            ],
            tcp_test(test_host, CORE_PORT),
        )
    )

    results.append(
        run_test(
            "Web server TCP connection",
            [
                f"Host: {test_host}",
                f"Port: {WEB_PORT}",
                "Expected: TCP connection succeeds",
            ],
            tcp_test(test_host, WEB_PORT),
        )
    )

    results.append(
        run_test(
            "Status page",
            [
                f"URL: http://{test_host}{STATUS_PATH}",
                "Expected: HTTP 200",
                f"Expected content: {EXPECTED_STATUS_TEXT!r}",
            ],
            status_page_test(test_host),
        )
    )

    return results


def ssh_target(ip):
    return f"{SSH_USER}@{ip}"


def run_ssh(ip, command, *, check=True, input_text=None, timeout=30):
    completed = subprocess.run(
        ["ssh", ssh_target(ip), command],
        input=input_text,
        text=True,
        capture_output=True,
        timeout=timeout,
    )

    if check and completed.returncode != 0:
        detail = (completed.stderr or completed.stdout or "").strip()
        raise Exception(
            f"SSH command failed ({completed.returncode}): {command}\n{detail}"
        )

    return completed


def remote_python(ip, script):
    completed = subprocess.run(
        ["ssh", ssh_target(ip), "sudo", "python3", "-"],
        input=script,
        text=True,
        capture_output=True,
        timeout=30,
    )

    if completed.returncode != 0:
        detail = (completed.stderr or completed.stdout or "").strip()
        raise Exception(f"Remote Python failed: {detail}")

    return completed.stdout


def wait_for_port(host, port, timeout, description):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            with socket.create_connection((host, port), timeout=2):
                return
        except OSError:
            time.sleep(2)

    raise Exception(f"Timed out waiting for {description} ({host}:{port})")


def wait_for_port_down(host, port, timeout):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            with socket.create_connection((host, port), timeout=1):
                time.sleep(1)
        except OSError:
            return


def reboot_and_wait(ip, settle_seconds):
    print("      Rebooting unit...")
    run_ssh(ip, "sudo reboot", check=False, timeout=10)

    time.sleep(3)
    wait_for_port_down(ip, 22, 30)
    wait_for_port(ip, 22, REBOOT_TIMEOUT_SECONDS, "SSH after reboot")
    wait_for_port(ip, CORE_PORT, REBOOT_TIMEOUT_SECONDS, "Core after reboot")

    print(f"      Core is reachable; settling for {settle_seconds} seconds...")
    time.sleep(settle_seconds)


def backup_properties(ip):
    command = (
        f"sudo rm -f {PROPERTIES_BACKUP_PATH} && "
        f"sudo cp -p {PROPERTIES_PATH} {PROPERTIES_BACKUP_PATH}"
    )
    run_ssh(ip, command)


def properties_sha256(ip, path):
    result = run_ssh(ip, f"sha256sum {path}")
    return result.stdout.split()[0]


def restore_properties(ip):
    command = (
        f"test -f {PROPERTIES_BACKUP_PATH} && "
        f"sudo cp -p {PROPERTIES_BACKUP_PATH} {PROPERTIES_PATH} && "
        f"sudo rm -f {PROPERTIES_BACKUP_PATH}"
    )
    run_ssh(ip, command)


def set_logger_credentials(ip, server, user, password):
    script = f'''
import json
import os

path = {PROPERTIES_PATH!r}
tmp = path + ".regression-tmp"

with open(path, "r", encoding="utf-8-sig") as f:
    root = json.load(f)

logger = root.setdefault("logger", {{}})
logger["server"] = {server!r}
logger["user"] = {user!r}
logger["password"] = {password!r}

with open(tmp, "w", encoding="utf-8") as f:
    json.dump(root, f, indent=2)
    f.write("\\n")

os.replace(tmp, path)
'''
    remote_python(ip, script)


def read_remote_identity(ip):
    script = f'''
import json

with open({PROPERTIES_PATH!r}, "r", encoding="utf-8-sig") as f:
    root = json.load(f)

print((root.get("network") or {{}}).get("hostname") or "")
print((root.get("global") or {{}}).get("systemSerialNumber") or "")
'''
    lines = remote_python(ip, script).splitlines()
    hostname = lines[0].strip() if len(lines) > 0 else ""
    serial = lines[1].strip() if len(lines) > 1 else ""
    return hostname, serial


def open_database():
    return mysql.connector.connect(
        host=DB_HOST,
        port=DB_PORT,
        database=DB_NAME,
        user=DB_USER,
        password=DB_PASSWORD,
        connection_timeout=5,
    )


def get_latest_session(hostname, serial):
    connection = open_database()
    try:
        cursor = connection.cursor(dictionary=True)
        try:
            cursor.execute(
                """
                SELECT id, startedat, lastheartbeatat,
                       hostname, serialnumber, launcherpid
                FROM logsession
                WHERE hostname = %s AND serialnumber = %s
                ORDER BY id DESC
                LIMIT 1
                """,
                (hostname, serial),
            )
            return cursor.fetchone()
        except mysql.connector.Error:
            cursor.close()
            cursor = connection.cursor(dictionary=True)
            cursor.execute(
                """
                SELECT id, startedat, lastheartbeatat,
                       hostname, launcherpid
                FROM logsession
                WHERE hostname = %s
                ORDER BY id DESC
                LIMIT 1
                """,
                (hostname,),
            )
            return cursor.fetchone()
    finally:
        connection.close()


def get_session_records(session_id):
    connection = open_database()
    try:
        cursor = connection.cursor(dictionary=True)
        cursor.execute(
            """
            SELECT timestamp, level, app, source, category, event, message
            FROM logrecord
            WHERE sessionid = %s
            ORDER BY timestamp ASC
            """,
            (session_id,),
        )
        return cursor.fetchall()
    finally:
        connection.close()


def show_latest_session(session, records):
    print()
    print("      LATEST LOG SESSION")
    print(f"      Session ID: {session.get('id')}")
    print(f"      Started: {session.get('startedat')}")
    print(f"      Last heartbeat: {session.get('lastheartbeatat')}")
    print(f"      Hostname: {session.get('hostname')}")
    if "serialnumber" in session:
        print(f"      Serial: {session.get('serialnumber')}")
    print(f"      Launcher PID: {session.get('launcherpid')}")
    print(f"      Records: {len(records)}")
    print()

    print("      Latest records:")
    for record in records[-30:]:
        print(
            "      "
            f"{record.get('timestamp')} "
            f"[{record.get('level')}] "
            f"{record.get('app')} "
            f"{record.get('category')}/{record.get('event')} "
            f"{record.get('message')}"
        )


def analyze_good_database_session(session, records):
    issues = []

    if not session:
        return ["No log session was created."]

    if not records:
        return ["The latest session contains no log records."]

    apps = {str(r.get("app") or "") for r in records}
    missing_apps = sorted(EXPECTED_APPS - apps)
    if missing_apps:
        issues.append(
            "Missing expected application logs: " + ", ".join(missing_apps)
        )

    startup_apps = {
        str(r.get("app") or "")
        for r in records
        if str(r.get("category") or "").lower() == "lifecycle"
        and str(r.get("event") or "").lower() == "startup"
    }
    missing_startups = sorted(EXPECTED_APPS - startup_apps)
    if missing_startups:
        issues.append(
            "Missing Startup records: " + ", ".join(missing_startups)
        )

    for record in records:
        level = str(record.get("level") or "").upper()
        app = str(record.get("app") or "")
        category = str(record.get("category") or "")
        event = str(record.get("event") or "")
        message = str(record.get("message") or "")
        combined = f"{app} {category} {event} {message}".lower()

        if level == "ERROR":
            expected_update_404 = (
                app == "SigCoreUpdater"
                and event == "PreparationFailed"
                and "404" in message
            )
            if not expected_update_404:
                issues.append(
                    f"ERROR log: {app} {category}/{event}: {message}"
                )

        if (
            category.lower() == "process"
            and event.lower() in {"restarted", "restartfailed"}
        ):
            issues.append(
                f"Application process problem: {app} {event}: {message}"
            )

        if "unhandled exception" in combined:
            issues.append(f"Unhandled exception reported: {app}: {message}")

    return issues


def clear_fallback_log(ip):
    run_ssh(ip, f"sudo rm -f {FALLBACK_LOG_PATH}", check=False)


def read_fallback_log(ip):
    result = run_ssh(
        ip,
        f"if test -f {FALLBACK_LOG_PATH}; then cat {FALLBACK_LOG_PATH}; fi",
        check=False,
        timeout=30,
    )
    return result.stdout or ""


def check_runtime_health(ip):
    issues = []

    process_status = run_ssh(
        ip,
        "ps -eo args | grep -E "
        "'/opt/sigcore/SigCoreLauncher|/opt/sigcore/current/sigcoreserver|"
        "/opt/sigcore/current/WebBridge|/opt/sigcore/current/SigCoreUpdater' "
        "| grep -v grep",
        check=False,
    ).stdout

    for process in (
        "SigCoreLauncher",
        "sigcoreserver",
        "WebBridge",
        "SigCoreUpdater",
    ):
        if process not in process_status:
            issues.append(f"Expected process is not running: {process}")

    try:
        with socket.create_connection((ip, CORE_PORT), timeout=3):
            pass
    except OSError as e:
        issues.append(f"Core is not reachable on port {CORE_PORT}: {e}")

    try:
        status_page_test(ip)()
    except Exception as e:
        issues.append(f"Status page failed: {e}")

    return issues


def analyze_bad_credentials(ip, log_text):
    issues = []

    if not log_text.strip():
        return [f"Fallback log was not created at {FALLBACK_LOG_PATH}."]

    lower = log_text.lower()

    expected_failure_found = any(
        token in lower
        for token in (
            "access denied",
            "database write failed",
            "session creation failed",
            "session lookup failed",
            "session heartbeat failed",
        )
    )

    if not expected_failure_found:
        issues.append(
            "Fallback log exists, but no database credential failure was recorded."
        )

    for token in (
        "unhandled exception",
        "restartfailed",
        "outofmemory",
        "segmentation fault",
    ):
        if token in lower:
            issues.append(f"Fallback log contains: {token}")

    nonblank_lines = [line for line in log_text.splitlines() if line.strip()]
    if len(nonblank_lines) > 80:
        issues.append(
            f"Fallback log is excessively noisy: {len(nonblank_lines)} lines "
            f"after {BAD_LOG_WAIT_SECONDS} seconds."
        )

    issues.extend(check_runtime_health(ip))
    return issues



def analyze_bad_address(ip, log_text):
    issues = []

    if not log_text.strip():
        return [f"Fallback log was not created at {FALLBACK_LOG_PATH}."]

    lower = log_text.lower()

    expected_timeout_found = any(
        token in lower
        for token in (
            "connect timeout expired",
            "connection timed out",
            "unable to connect",
            "connection refused",
            "session creation failed",
            "session lookup failed",
            "session heartbeat failed",
        )
    )

    if not expected_timeout_found:
        issues.append(
            "Fallback log exists, but no unreachable-database failure was recorded."
        )

    for token in (
        "unhandled exception",
        "restartfailed",
        "outofmemory",
        "segmentation fault",
    ):
        if token in lower:
            issues.append(f"Fallback log contains: {token}")

    nonblank_lines = [line for line in log_text.splitlines() if line.strip()]
    if len(nonblank_lines) > 80:
        issues.append(
            f"Fallback log is excessively noisy: {len(nonblank_lines)} lines "
            f"after {BAD_ADDRESS_WAIT_SECONDS} seconds."
        )

    issues.extend(check_runtime_health(ip))
    return issues


def core_protocol_smoke(ip):
    """
    Lightweight protocol-path check: establish several short-lived connections.
    This is stronger than a single TCP accept, while remaining protocol-agnostic.
    """
    failures = []
    for i in range(5):
        started = time.time()
        try:
            with socket.create_connection((ip, CORE_PORT), timeout=3):
                elapsed = time.time() - started
                if elapsed > 2.0:
                    failures.append(
                        f"Core connection {i + 1} took {elapsed:.2f}s."
                    )
        except OSError as e:
            failures.append(f"Core connection {i + 1} failed: {e}")
        time.sleep(1)

    return failures


def logging_regression_test(ip):
    print()
    print("==================================================")
    print("LOGGING REGRESSION")
    print("==================================================")
    print(f"Target: {ip}")
    print()

    overall_ok = True
    original_hash = None
    restore_needed = False

    try:
        print("STEP  1  Back up properties.json")
        backup_properties(ip)
        restore_needed = True
        original_hash = properties_sha256(ip, PROPERTIES_BACKUP_PATH)
        print(f"      Backup SHA256: {original_hash}")
        print("PASS  Backup")
        print()

        print("STEP  2  Configure valid logging database")
        set_logger_credentials(ip, DB_HOST, DB_USER, DB_PASSWORD)
        print(f"      Server: {DB_HOST}")
        print(f"      User: {DB_USER}")
        print("PASS  Valid DB configuration written")
        print()

        reboot_and_wait(ip, GOOD_LOG_WAIT_SECONDS)

        print("STEP  3  Verify database logging")
        hostname, serial = read_remote_identity(ip)
        if not hostname:
            raise Exception("properties.json has no network.hostname")
        if not serial:
            raise Exception("properties.json has no global.systemSerialNumber")

        session = get_latest_session(hostname, serial)
        if not session:
            raise Exception(
                f"No log session found for hostname={hostname} serial={serial}"
            )

        records = get_session_records(session["id"])
        show_latest_session(session, records)

        issues = analyze_good_database_session(session, records)
        if issues:
            overall_ok = False
            print()
            print("FAIL  Database logging health")
            for issue in issues:
                print(f"      {issue}")
        else:
            print()
            print("PASS  Database logging health")
            print("      All expected apps logged Startup.")
            print("      No unexpected ERROR records or process failures found.")
        print()

        print("STEP  4  Configure intentionally wrong DB password")
        clear_fallback_log(ip)
        set_logger_credentials(
            ip,
            DB_HOST,
            DB_USER,
            "REGRESSION_INTENTIONALLY_WRONG_PASSWORD",
        )
        print("PASS  Bad credentials written")
        print()

        reboot_and_wait(ip, BAD_LOG_WAIT_SECONDS)

        print("STEP  5  Verify fallback logging and system survival")
        fallback = read_fallback_log(ip)

        print(f"      Fallback file: {FALLBACK_LOG_PATH}")
        print(
            f"      Fallback lines: "
            f"{len([x for x in fallback.splitlines() if x.strip()])}"
        )
        print()
        print("      Tail of fallback log:")
        for line in fallback.splitlines()[-30:]:
            print(f"      {line}")

        bad_issues = analyze_bad_credentials(ip, fallback)
        if bad_issues:
            overall_ok = False
            print()
            print("FAIL  Bad-credential behavior")
            for issue in bad_issues:
                print(f"      {issue}")
        else:
            print()
            print("PASS  Bad-credential behavior")
            print("      DB failure was logged.")
            print("      Core, Launcher, Updater and WebBridge remain running.")
            print("      Core and status page remain reachable.")
            print("      Fallback logging is not excessively noisy.")
        print()

        print("STEP  6  Configure unreachable DB address")
        clear_fallback_log(ip)
        set_logger_credentials(
            ip,
            BAD_DB_HOST,
            DB_USER,
            DB_PASSWORD,
        )
        print(f"      Server: {BAD_DB_HOST}")
        print("PASS  Unreachable DB address written")
        print()

        reboot_and_wait(ip, BAD_ADDRESS_WAIT_SECONDS)

        print("STEP  7  Verify timeout handling and system survival")
        fallback = read_fallback_log(ip)

        print(f"      Fallback file: {FALLBACK_LOG_PATH}")
        print(f"      Fallback lines: {len([x for x in fallback.splitlines() if x.strip()])}")
        print()
        print("      Tail of fallback log:")
        for line in fallback.splitlines()[-30:]:
            print(f"      {line}")

        bad_address_issues = analyze_bad_address(ip, fallback)
        bad_address_issues.extend(core_protocol_smoke(ip))

        if bad_address_issues:
            overall_ok = False
            print()
            print("FAIL  Unreachable-DB behavior")
            for issue in bad_address_issues:
                print(f"      {issue}")
        else:
            print()
            print("PASS  Unreachable-DB behavior")
            print("      Timeout/failure was recorded.")
            print("      Core, Launcher, Updater and WebBridge remain running.")
            print("      Core and status page remain reachable.")
            print("      Repeated Core connections remain responsive.")
            print("      Fallback logging is not excessively noisy.")
        print()

    except Exception as e:
        overall_ok = False
        print(f"FAIL  Logging regression aborted: {e}")
        print()

    finally:
        if restore_needed:
            print("CLEANUP")
            try:
                restore_properties(ip)

                restored_hash = properties_sha256(ip, PROPERTIES_PATH)
                if original_hash and restored_hash != original_hash:
                    overall_ok = False
                    print("FAIL  properties.json restore verification")
                    print(f"      Expected SHA256: {original_hash}")
                    print(f"      Actual SHA256:   {restored_hash}")
                else:
                    print("PASS  properties.json restored exactly")

                clear_fallback_log(ip)
                print("PASS  Regression fallback log removed")

                reboot_and_wait(ip, 10)
                health_issues = check_runtime_health(ip)
                if health_issues:
                    overall_ok = False
                    print("FAIL  Post-cleanup runtime health")
                    for issue in health_issues:
                        print(f"      {issue}")
                else:
                    print("PASS  Post-cleanup runtime health")
            except Exception as e:
                overall_ok = False
                print(f"FAIL  Cleanup: {e}")

            print()

    if overall_ok:
        print("LOGGING REGRESSION: PASS")
    else:
        print("LOGGING REGRESSION: FAIL")

    return overall_ok


def parse_args():
    parser = argparse.ArgumentParser(
        description="SigCore UC regression test"
    )
    parser.add_argument(
        "--logging-ip",
        metavar="IP",
        help=(
            "Run the logging regression against this unit. "
            "The test backs up and restores /opt/sigcore/properties.json "
            "and reboots the unit several times."
        ),
    )
    return parser.parse_args()


def main():
    args = parse_args()
    units = discover_sigcore_units()

    if not units:
        print("FAIL  SigCore discovery")
        print(f"      No services found for {SERVICE_TYPE}")
        print()
        print("OVERALL: FAIL")
        sys.exit(1)

    print("PASS  SigCore discovery")
    print(f"      Found {len(units)} unit(s)")
    print()

    all_results = []

    for unit in units:
        all_results.extend(test_unit(unit))

    logging_ok = True
    if args.logging_ip:
        logging_ok = logging_regression_test(args.logging_ip)

    print()
    print("=======================")
    passed = sum(all_results)
    total = len(all_results)

    print(f"Units found: {len(units)}")
    print(f"Basic result: {passed}/{total} executed tests passed")
    if args.logging_ip:
        print(f"Logging regression: {'PASS' if logging_ok else 'FAIL'}")

    basic_ok = bool(all_results) and all(all_results)

    if basic_ok and logging_ok:
        print("OVERALL: PASS")
        sys.exit(0)

    print("OVERALL: FAIL")
    sys.exit(1)


if __name__ == "__main__":
    main()
