import argparse
import json
import math
import struct
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
HOME_PATH = "/"

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


def home_page_test(host):
    def run():
        url = f"http://{host}{HOME_PATH}"
        try:
            with urllib.request.urlopen(url, timeout=5) as response:
                status = response.status
                if status != 200:
                    raise Exception(f"HTTP {status}")

                return f"Actual: HTTP {status}"

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
            "Home page",
            [
                f"URL: http://{test_host}{HOME_PATH}",
                "Expected: HTTP 200",
            ],
            home_page_test(test_host),
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
        home_page_test(ip)()
    except Exception as e:
        issues.append(f"Home page failed: {e}")

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


# ---------------------------------------------------------------------------
# Modbus TCP regression. This is intentionally implemented with the standard
# library: no pymodbus dependency and no benchmark/endurance tests.
# The public Modbus mapping contains separate command and feedback locations.
# ---------------------------------------------------------------------------

MODBUS_PORT = 502
MODBUS_UNIT_ID = 1
MODBUS_FEEDBACK_TIMEOUT = 8
# The wired I/O regression accepts 0.15 V deviation on AIN loopback.
# AOUT feedback is Core's reported setpoint and must be much closer.
MODBUS_AIN_LOOPBACK_TOLERANCE = 0.15
MODBUS_AOUT_FEEDBACK_TOLERANCE = 0.01
MODBUS_LOOPBACK_VALUES = (0.0, 1.25, 2.5, 3.75, 5.0)
MODBUS_OUTPUT_FEEDBACK_TIMEOUT = 8
MODBUS_INPUT_SETTLE_TIMEOUT = 10


class ModbusException(Exception):
    def __init__(self, function, code):
        super().__init__(f"Modbus FC {function:02X} exception {code:02X}")
        self.function = function
        self.code = code


class ModbusSkip(Exception):
    """An optional observation could not be exercised on this test device."""


class ModbusClient:
    def __init__(self, host):
        self.host = host
        self.sock = None
        self.transaction_id = 0

    def __enter__(self):
        self.sock = socket.create_connection((self.host, MODBUS_PORT), timeout=5)
        self.sock.settimeout(5)
        return self

    def __exit__(self, exc_type, exc, tb):
        self.sock.close()
        self.sock = None

    def _read_exact(self, length):
        chunks = bytearray()
        while len(chunks) < length:
            data = self.sock.recv(length - len(chunks))
            if not data:
                raise ConnectionError("Modbus TCP connection closed during response")
            chunks.extend(data)
        return bytes(chunks)

    def request(self, pdu):
        self.transaction_id = (self.transaction_id + 1) & 0xFFFF
        transaction = self.transaction_id
        packet = struct.pack(">HHHB", transaction, 0, len(pdu) + 1,
                             MODBUS_UNIT_ID) + pdu
        self.sock.sendall(packet)
        header = self._read_exact(7)
        rx_transaction, protocol, length, unit = struct.unpack(">HHHB", header)
        if (rx_transaction, protocol, unit) != (transaction, 0, MODBUS_UNIT_ID):
            raise AssertionError(
                f"Invalid MBAP header: transaction={rx_transaction}, "
                f"protocol={protocol}, unit={unit}"
            )
        if not 2 <= length <= 254:
            raise AssertionError(f"Invalid Modbus response length: {length}")
        response = self._read_exact(length - 1)
        if response[0] == (pdu[0] | 0x80):
            if len(response) != 2:
                raise AssertionError(f"Malformed exception response: {response.hex()}")
            raise ModbusException(pdu[0], response[1])
        if response[0] != pdu[0]:
            raise AssertionError(
                f"Unexpected function code {response[0]:02X} for {pdu[0]:02X}"
            )
        return response

    def expect_exception(self, pdu, code):
        try:
            self.request(pdu)
        except ModbusException as error:
            if error.code != code:
                raise AssertionError(
                    f"Expected Modbus exception {code:02X}, received {error.code:02X}"
                ) from error
            return
        raise AssertionError(f"Expected exception {code:02X} for {pdu.hex()}")

    def read_bits(self, address, quantity):
        pdu = struct.pack(">BHH", 2, address, quantity)
        reply = self.request(pdu)
        expected_bytes = (quantity + 7) // 8
        if len(reply) != 2 + expected_bytes or reply[1] != expected_bytes:
            raise AssertionError(f"FC02 invalid byte count: {reply.hex()}")
        return [bool(reply[2 + i // 8] & (1 << (i % 8)))
                for i in range(quantity)]

    def read_registers(self, address, quantity):
        pdu = struct.pack(">BHH", 4, address, quantity)
        reply = self.request(pdu)
        if len(reply) != quantity * 2 + 2 or reply[1] != quantity * 2:
            raise AssertionError(f"FC04 invalid register count: {reply.hex()}")
        return struct.unpack(">" + "H" * quantity, reply[2:])

    def read_float(self, address):
        words = self.read_registers(address, 2)
        return struct.unpack(">f", struct.pack(">HH", *words))[0]

    def write_relay(self, channel, state):
        pdu = struct.pack(">BHH", 5, channel, 0xFF00 if state else 0)
        if self.request(pdu) != pdu:
            raise AssertionError("FC05 command echo mismatch")

    def write_analog_output(self, channel, value):
        pdu = struct.pack(">BHHBf", 16, 2 * channel, 2, 4, float(value))
        if self.request(pdu) != pdu[:5]:
            raise AssertionError("FC16 command acknowledgement mismatch")


def modbus_test(name, details, function):
    """Use the existing PASS/FAIL format, also supporting honest SKIP results."""
    print(f"TEST  {name}")
    for detail in details:
        print(f"      {detail}")
    try:
        message = function()
        if message:
            print(f"      {message}")
        print(f"PASS  {name}\n")
        return True
    except ModbusSkip as error:
        print(f"      Reason: {error}\nSKIP  {name}\n")
        return None
    except Exception as error:
        print(f"      Actual: {error}\nFAIL  {name}\n")
        return False


def modbus_read_feedback(client):
    bits = client.read_bits(0, 16)
    words = client.read_registers(0, 16)
    floats = [struct.unpack(">f", struct.pack(">HH", *words[i:i + 2]))[0]
              for i in range(0, 16, 2)]
    if len(bits) != 16 or len(floats) != 8:
        raise AssertionError("Incomplete Modbus I/O feedback")
    if not all(math.isfinite(value) for value in floats):
        raise AssertionError("Modbus feedback contains non-finite analog values")
    return {"di": bits[:8], "relay": bits[8:],
            "ai": floats[:4], "ao": floats[4:]}


def modbus_wait_ready(client, timeout=MODBUS_FEEDBACK_TIMEOUT):
    deadline = time.monotonic() + timeout
    last_error = None
    while time.monotonic() < deadline:
        try:
            return modbus_read_feedback(client)
        except ModbusException as error:
            if error.code != 4:
                raise
            last_error = error
            time.sleep(.25)
    raise AssertionError(f"Core feedback not initialized: {last_error}")


def modbus_wait_value(read_value, expected, tolerance=0.0, timeout=5):
    deadline = time.monotonic() + timeout
    actual = None
    while time.monotonic() < deadline:
        try:
            actual = read_value()
            if (abs(actual - expected) <= tolerance
                    if isinstance(expected, float) else actual == expected):
                return actual
        except ModbusException as error:
            if error.code != 4:
                raise
        time.sleep(.15)
    raise AssertionError(f"Feedback did not reach {expected!r}; last={actual!r}")


def modbus_read_state_from_webbridge(ip):
    # WebBridge binds only to 127.0.0.1:8080 on the Pi. Query via existing
    # SSH utility; no new service, files, or network endpoints are needed.
    script = '''import urllib.request
with urllib.request.urlopen("http://127.0.0.1:8080/api/state", timeout=5) as r:
    print(r.read().decode("utf-8"))'''
    state = json.loads(remote_python(ip, script))
    if not state.get("connection", {}).get("connected"):
        raise AssertionError("WebBridge is not connected to Core")
    expected = {"di": state.get("digitalInputs"),
                "relay": state.get("relays"),
                "ai": state.get("analogInputs"),
                "ao": state.get("analogOutputs", {}).get("values")}
    for kind, values in expected.items():
        count = 8 if kind in ("di", "relay") else 4
        if not isinstance(values, list) or len(values) < count:
            raise AssertionError(f"Incomplete WebBridge {kind} snapshot")
        expected[kind] = values[:count]
    return expected, state


def modbus_write_via_webbridge(ip, kind, channel, value):
    path = "/api/control/relay" if kind == "relay" else "/api/control/analog-output"
    payload = json.dumps({"channel": channel,
                          "state" if kind == "relay" else "value": value})
    script = ("import json, urllib.request\n"
              f"data = {payload!r}.encode('utf-8')\n"
              f"req = urllib.request.Request('http://127.0.0.1:8080{path}', "
              "data=data, headers={'Content-Type':'application/json'}, "
              "method='POST')\n"
              "with urllib.request.urlopen(req, timeout=5) as r:\n"
              "    print(r.read().decode('utf-8'))\n")
    answer = json.loads(remote_python(ip, script))
    if not answer.get("accepted"):
        raise AssertionError(f"WebBridge rejected {kind} command: {answer}")


def modbus_protocol_tests(host):
    results = []

    results.append(modbus_test(
        "Modbus TCP port 502", [f"Host: {host}", "Expected: TCP connection succeeds"],
        tcp_test(host, MODBUS_PORT)))

    def valid_discrete():
        with ModbusClient(host) as mb:
            bits = mb.read_bits(0, 16)
            if len(bits) != 16 or not all(isinstance(x, bool) for x in bits):
                raise AssertionError("Invalid discrete feedback")
            # Separate reads can straddle a legitimate relay transition.
            if len(mb.read_bits(8, 8)) != 8:
                raise AssertionError("Incomplete relay feedback")
            return f"DIN: {bits[:8]}; relays: {bits[8:]}"

    results.append(modbus_test("Read DIN and relay feedback (FC02)",
                               ["Discrete input addresses 0-15"], valid_discrete))

    def valid_analog():
        with ModbusClient(host) as mb:
            full = modbus_wait_ready(mb)
            for channel in range(4):
                # Individual float reads verify register address and word order.
                for name, base in (("AIN", 0), ("AOUT", 8)):
                    value = mb.read_float(base + channel * 2)
                    if not math.isfinite(value):
                        raise AssertionError(f"{name}{channel+1} is non-finite")
            return f"AIN: {full['ai']}; AOUT feedback: {full['ao']}"

    results.append(modbus_test("Read AIN and AOUT feedback (FC04)",
                               ["Input register addresses 0-15; IEEE-754 floats"],
                               valid_analog))

    def illegal_reads():
        cases = [
            (struct.pack(">BHH", 2, 16, 1), 2),
            (struct.pack(">BHH", 2, 15, 2), 2),
            (struct.pack(">BHH", 2, 0, 0), 3),
            (struct.pack(">BHH", 4, 15, 2), 2),
            (struct.pack(">BHH", 4, 0, 0), 3),
            (struct.pack(">BHH", 1, 0, 8), 1),  # command coils write-only
            (struct.pack(">BHH", 3, 0, 8), 1),  # holding registers write-only
            (struct.pack(">BHH", 6, 0, 0), 1),  # unsupported write function
        ]
        with ModbusClient(host) as mb:
            for pdu, code in cases:
                mb.expect_exception(pdu, code)
        return f"Verified {len(cases)} illegal read/unsupported-function exceptions"

    results.append(modbus_test("Invalid read addresses and unsupported functions",
                               ["Expect Modbus exceptions 01, 02, 03"], illegal_reads))

    def persistent_clients():
        with ModbusClient(host) as first, ModbusClient(host) as second:
            a = first.read_bits(0, 16)
            b = second.read_bits(0, 16)
            first.read_registers(0, 16)
            second.read_registers(0, 16)
            # A live digital input may change between snapshots; both clients
            # must answer valid requests, not necessarily hold identical values.
            if len(a) != 16 or len(b) != 16:
                raise AssertionError("Incomplete multi-client response")
        return "Two simultaneous clients completed multiple requests"

    results.append(modbus_test("Persistent and simultaneous Modbus clients",
                               ["Two connections, no performance/endurance testing"],
                               persistent_clients))
    return results


def modbus_invalid_write_tests(host):
    def illegal_writes():
        # Only invalid commands are sent; none should change any output.
        cases = [
            (struct.pack(">BHH", 5, 8, 0xFF00), 2),
            (struct.pack(">BHH", 5, 0, 0x0001), 3),
            (struct.pack(">BHHBf", 16, 1, 2, 4, 1.0), 2),
            (struct.pack(">BHHBf", 16, 8, 2, 4, 1.0), 2),
            (struct.pack(">BHH", 16, 0, 1) + b"\x02\x00\x00", 3),
            (struct.pack(">BHHB", 16, 0, 2, 4) + b"\x7f\xc0\x00\x00", 3),
        ]
        with ModbusClient(host) as mb:
            for pdu, code in cases:
                mb.expect_exception(pdu, code)
        return f"Verified {len(cases)} invalid-command exceptions without changing outputs"

    return modbus_test("Invalid output commands",
                       ["Invalid requests only; no actual output writes"],
                       illegal_writes)


def modbus_compare_core(host, ip):
    def run():
        before, _ = modbus_read_state_from_webbridge(ip)
        with ModbusClient(host) as mb:
            live = modbus_wait_ready(mb)
        after, _ = modbus_read_state_from_webbridge(ip)
        checked, skipped = 0, 0
        for kind in ("di", "relay", "ai", "ao"):
            for index, value in enumerate(live[kind]):
                start, end = before[kind][index], after[kind][index]
                if kind in ("di", "relay"):
                    if start != end:
                        skipped += 1  # changed while snapshots were collected
                        continue
                    if value != start:
                        raise AssertionError(f"{kind}{index+1}: Modbus {value}, Core {start}")
                else:
                    if not all(math.isfinite(float(v)) for v in (start, end, value)):
                        raise AssertionError(f"{kind}{index+1}: non-finite feedback")
                    margin = max(.05, .02 * max(abs(start), abs(end)))
                    if not min(start, end) - margin <= value <= max(start, end) + margin:
                        raise AssertionError(
                            f"{kind}{index+1}: Modbus {value}, Core {start}..{end}"
                        )
                checked += 1
        if not checked:
            raise ModbusSkip("Every I/O channel changed while sampling")
        return f"Compared {checked} Core channels via WebBridge; {skipped} changing channels skipped"

    return modbus_test("Modbus feedback versus Core subscriptions",
                       ["Source: WebBridge /api/state over existing SSH"], run)


def modbus_relay_write_test(host, ip, channel):
    def run():
        with ModbusClient(host) as mb:
            original = mb.read_bits(8 + channel, 1)[0]
            write_attempted = False
            try:
                write_attempted = True
                mb.write_relay(channel, not original)
                modbus_wait_value(lambda: mb.read_bits(8 + channel, 1)[0], not original)
                # Separate command source, same Core subscription feedback.
                modbus_write_via_webbridge(ip, "relay", channel, original)
                modbus_wait_value(lambda: mb.read_bits(8 + channel, 1)[0], original)
            finally:
                if write_attempted:
                    # Restore even if the write or feedback test fails.
                    mb.write_relay(channel, original)
                    modbus_wait_value(lambda: mb.read_bits(8 + channel, 1)[0], original)
        return (f"Relay {channel+1}: Modbus changed output, WebBridge restored it; "
                "final feedback verified")

    return modbus_test("Relay command and cross-interface feedback",
                       [f"Relay {channel+1}: physical output will be operated and restored"],
                       run)


def modbus_ao_write_test(host, ip, channel, requested):
    def run():
        _, state = modbus_read_state_from_webbridge(ip)
        modes = state["analogOutputs"].get("auto", [])
        if len(modes) <= channel:
            raise AssertionError("Unable to check AOUT auto/manual mode")
        if modes[channel]:
            raise ModbusSkip(f"AOUT{channel+1} is in automatic mode")
        with ModbusClient(host) as mb:
            original = mb.read_float(8 + 2 * channel)
            if not math.isfinite(original):
                raise AssertionError("Original analog output value is invalid")
            if abs(requested - original) < max(.0001, abs(original)*.00001):
                raise ModbusSkip("Specified analog value matches current output")
            commanded = struct.unpack(">f", struct.pack(">f", requested))[0]
            tolerance = max(.001, abs(commanded) * .0001)
            write_attempted = False
            try:
                write_attempted = True
                mb.write_analog_output(channel, requested)
                modbus_wait_value(lambda: mb.read_float(8 + 2*channel),
                                  commanded, tolerance)
                modbus_write_via_webbridge(ip, "ao", channel, original)
                modbus_wait_value(lambda: mb.read_float(8 + 2*channel),
                                  original, tolerance)
            finally:
                if write_attempted:
                    mb.write_analog_output(channel, original)
                    modbus_wait_value(lambda: mb.read_float(8 + 2*channel),
                                      original, tolerance)
        return (f"AOUT{channel+1} set to {commanded}, restored to {original}; "
                "Core feedback verified")

    return modbus_test("Analog output command and cross-interface feedback",
                       [f"AOUT{channel+1}: physical output will be changed and restored"],
                       run)


def modbus_all_relay_test(host, channel):
    """Exercise one relay through both logical states and restore it."""
    def run():
        with ModbusClient(host) as mb:
            address = 8 + channel  # FC02 relay feedback, not command coil.
            original = mb.read_bits(address, 1)[0]
            failures = []
            try:
                if channel == 0:
                    initial_din = mb.read_bits(0, 1)[0]
                    print(f"      Relay 1 initial={original}; DIN1 initial={initial_din}")
                    if initial_din != original:
                        failures.append(
                            f"initial DIN1={initial_din} differs from relay 1={original}")

                for requested in (not original, original):
                    try:
                        print(f"      COMMAND relay {channel + 1} -> {requested} (FC05)", flush=True)
                        mb.write_relay(channel, requested)  # FC05.
                        relay = modbus_wait_value(
                            lambda: mb.read_bits(address, 1)[0], requested,
                            timeout=MODBUS_OUTPUT_FEEDBACK_TIMEOUT)
                        if channel == 0:
                            din = modbus_wait_value(
                                lambda: mb.read_bits(0, 1)[0], requested,
                                timeout=MODBUS_INPUT_SETTLE_TIMEOUT)
                            print(f"      Relay 1={relay}; DIN1={din}")
                        else:
                            print(f"      Relay {channel + 1}={relay}")
                    except Exception as error:
                        failures.append(f"relay {channel + 1} set to {requested}: {error}")
                        print(f"      Relay {channel + 1} requested={requested}: {error}")
            finally:
                # Restore even if the command/feedback tests failed.
                try:
                    mb.write_relay(channel, original)
                    modbus_wait_value(
                        lambda: mb.read_bits(address, 1)[0], original,
                        timeout=MODBUS_OUTPUT_FEEDBACK_TIMEOUT)
                    if channel == 0:
                        modbus_wait_value(
                            lambda: mb.read_bits(0, 1)[0], original,
                            timeout=MODBUS_INPUT_SETTLE_TIMEOUT)
                except Exception as error:
                    failures.append(f"relay {channel + 1} restoration: {error}")

            if failures:
                raise AssertionError("; ".join(failures))
        return f"Relay {channel + 1} toggled, feedback verified, original state restored"

    return modbus_test(
        f"Toggle relay {channel + 1} and verify feedback" +
        (" and DIN1" if channel == 0 else ""),
        [f"FC05 coil {channel}; FC02 relay feedback {8 + channel}" +
         ("; FC02 DIN1 at 0" if channel == 0 else ""),
         "Toggle to the opposite state and back; restore original state"],
        run)


def modbus_ao_loopback_test(host, channel):
    """Step AOUT through 0..5 V; check feedback and corresponding wired AIN."""
    def run():
        with ModbusClient(host) as mb:
            ao_address = 8 + 2 * channel  # FC04 feedback pair.
            ai_address = 2 * channel      # FC04 corresponding AIN pair.
            original = mb.read_float(ao_address)
            if not math.isfinite(original):
                raise AssertionError(f"AOUT{channel + 1} original value is not finite")
            failures = []
            try:
                for requested in MODBUS_LOOPBACK_VALUES:
                    # The command is transmitted as IEEE-754 float32.
                    commanded = struct.unpack(">f", struct.pack(">f", requested))[0]
                    try:
                        print(f"      COMMAND AOUT{channel + 1} -> {requested:.2f} V (FC16)", flush=True)
                        mb.write_analog_output(channel, commanded)  # FC16.
                        ao = modbus_wait_value(
                            lambda: mb.read_float(ao_address), commanded,
                            tolerance=MODBUS_AOUT_FEEDBACK_TOLERANCE,
                            timeout=MODBUS_OUTPUT_FEEDBACK_TIMEOUT)
                        ai = modbus_wait_value(
                            lambda: mb.read_float(ai_address), commanded,
                            tolerance=MODBUS_AIN_LOOPBACK_TOLERANCE,
                            timeout=MODBUS_INPUT_SETTLE_TIMEOUT)
                        print(f"      AOUT{channel + 1} set={requested:.2f} V; "
                              f"feedback={ao:.4f} V; AIN{channel + 1}={ai:.4f} V; "
                              f"difference={ai - commanded:+.4f} V")
                    except Exception as error:
                        failures.append(f"{requested:.2f} V: {error}")
                        # Record the channel readings even when feedback has not
                        # converged, so the mismatch can be diagnosed.
                        try:
                            ao = mb.read_float(ao_address)
                            ai = mb.read_float(ai_address)
                            print(f"      AOUT{channel + 1} set={requested:.2f} V; "
                                  f"feedback={ao:.4f} V; AIN{channel + 1}={ai:.4f} V; "
                                  f"FAIL: {error}")
                        except Exception:
                            print(f"      AOUT{channel + 1} set={requested:.2f} V; "
                                  f"FAIL: {error}")
            finally:
                try:
                    mb.write_analog_output(channel, original)
                    restored = modbus_wait_value(
                        lambda: mb.read_float(ao_address), original,
                        tolerance=MODBUS_AOUT_FEEDBACK_TOLERANCE,
                        timeout=MODBUS_OUTPUT_FEEDBACK_TIMEOUT)
                    print(f"      AOUT{channel + 1} restored={restored:.4f} V")
                except Exception as error:
                    failures.append(f"restoration to {original:.4f} V: {error}")

            if failures:
                raise AssertionError("; ".join(failures))
        return (f"AOUT{channel + 1}: all {len(MODBUS_LOOPBACK_VALUES)} setpoints "
                f"verified against feedback and AIN{channel + 1}; original restored")

    return modbus_test(
        f"AOUT{channel + 1} step test and AIN{channel + 1} loopback",
        ["FC16 command and FC04 AOUT/AIN feedback",
         "Voltages: " + ", ".join(f"{x:g}" for x in MODBUS_LOOPBACK_VALUES),
         f"AOUT tolerance: +/-{MODBUS_AOUT_FEEDBACK_TOLERANCE:.2f} V; "
         f"AIN tolerance: +/-{MODBUS_AIN_LOOPBACK_TOLERANCE:.2f} V",
         "Original output value restored after test"],
        run)


def modbus_observe_input_changes(host, ip, seconds):
    def run():
        initial, _ = modbus_read_state_from_webbridge(ip)
        previous = initial
        observed = set()
        deadline = time.monotonic() + seconds
        with ModbusClient(host) as mb:
            modbus_wait_ready(mb)
            print("      Stimulate DIN or AIN externally during the observation interval.")
            while time.monotonic() < deadline:
                time.sleep(min(.75, max(.01, deadline - time.monotonic())))
                current, _ = modbus_read_state_from_webbridge(ip)
                for kind in ("di", "ai"):
                    for index, value in enumerate(current[kind]):
                        old = previous[kind][index]
                        changed = (value != old if kind == "di" else
                                   abs(value - old) > max(.05, abs(old)*.02))
                        if not changed:
                            continue
                        address = index if kind == "di" else index*2
                        try:
                            if kind == "di":
                                modbus_wait_value(lambda: mb.read_bits(address, 1)[0],
                                                  value, timeout=3)
                            else:
                                modbus_wait_value(lambda: mb.read_float(address),
                                                  float(value),
                                                  tolerance=max(.05, abs(value)*.02),
                                                  timeout=3)
                            observed.add((kind, index))
                        except AssertionError as error:
                            raise AssertionError(f"{kind}{index+1}: {error}") from error
                previous = current
        if not observed:
            raise ModbusSkip("No DIN/AIN change observed; provide external stimulus")
        channels = ", ".join(f"{kind.upper()}{index+1}"
                             for kind, index in sorted(observed))
        return f"Core-originated changes reached Modbus feedback: {channels}"

    return modbus_test("External DIN/AIN subscription updates",
                       [f"Observe for {seconds} seconds; does not generate any output writes"],
                       run)


def modbus_core_outage_recovery(host):
    def run():
        with ModbusClient(host) as mb:
            modbus_wait_ready(mb)
            print("      Stop Core using your existing method, leaving ModbusBridge running.")
            input("      Press ENTER after Core has stopped: ")
            # A disconnected Core must not produce false cached feedback.
            mb.expect_exception(struct.pack(">BHH", 2, 0, 16), 4)
            mb.expect_exception(struct.pack(">BHH", 4, 0, 16), 4)
            print("      Restart Core using your existing method.")
            input("      Press ENTER after Core has restarted: ")
            modbus_wait_ready(mb, timeout=30)
        return "Stale values rejected on Core outage; full feedback restored"

    return modbus_test("Core outage and subscription resynchronization",
                       ["Interactive test; Core must stop independently of ModbusBridge"],
                       run)


def modbus_reboot_recovery(host, ip):
    def run():
        with ModbusClient(host) as mb:
            modbus_wait_ready(mb)
        reboot_and_wait(ip, 3)  # Existing regression reboot/SSH procedure.
        wait_for_port(host, MODBUS_PORT, REBOOT_TIMEOUT_SECONDS,
                      "ModbusBridge after reboot")
        with ModbusClient(host) as mb:
            current = modbus_wait_ready(mb, timeout=20)
        return ("ModbusBridge accepted requests after reboot; "
                f"all 8 DIN, 8 relays, 4 AIN and 4 AOUT feedback channels initialized. "
                f"Relay feedback: {current['relay']}")

    return modbus_test("Device restart and Modbus feedback resynchronization",
                       ["Reboots the Pi; no I/O commands are sent"],
                       run)


def modbus_regression(host, ip, args):
    print("\n==================================================")
    print("MODBUS REGRESSION")
    print("==================================================")
    print(f"Host: {host}; Port: {MODBUS_PORT}; Unit ID: {MODBUS_UNIT_ID}\n")
    results = modbus_protocol_tests(host)
    if args.modbus_compare:
        results.append(modbus_compare_core(host, ip))
    if args.modbus_observe_seconds:
        results.append(modbus_observe_input_changes(
            host, ip, args.modbus_observe_seconds))
    # An explicit --modbus-ip targets one controller for the wired I/O test.
    # --modbus alone remains read-only when discovering multiple devices.
    if args.modbus_ip or args.modbus_write:
        print("WIRED I/O TEST: commanding all 8 relays and stepping all 4 analog outputs "
              "(unless a single channel was selected). Original states will be restored.\n")
        results.append(modbus_invalid_write_tests(host))
        if args.modbus_relay is None and args.modbus_ao is None:
            # Complete wired-I/O regression for the dedicated test device.
            for channel in range(8):
                results.append(modbus_all_relay_test(host, channel))
            for channel in range(4):
                results.append(modbus_ao_loopback_test(host, channel))
        else:
            # Retain the existing targeted write tests.
            if args.modbus_relay is not None:
                results.append(modbus_relay_write_test(
                    host, ip, args.modbus_relay - 1))
            if args.modbus_ao is not None:
                results.append(modbus_ao_write_test(
                    host, ip, args.modbus_ao - 1, args.modbus_ao_value))
    if args.modbus_core_outage:
        results.append(modbus_core_outage_recovery(host))
    if args.modbus_reboot:
        results.append(modbus_reboot_recovery(host, ip))
    passed = sum(result is True for result in results)
    failed = sum(result is False for result in results)
    skipped = sum(result is None for result in results)
    print(f"MODBUS REGRESSION: {passed} PASS, {failed} FAIL, {skipped} SKIP")
    return failed == 0 and passed > 0


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
    parser.add_argument("--modbus", action="store_true",
                        help="Run read-only Modbus TCP tests on discovered controllers")
    parser.add_argument("--modbus-ip", metavar="IP",
                        help="Run Modbus tests and physically exercise all relays and AOUT/AIN "
                             "loops on this single controller")
    parser.add_argument("--modbus-compare", action="store_true",
                        help="Compare Modbus I/O with Core state through SSH and WebBridge")
    parser.add_argument("--modbus-observe-seconds", metavar="SECONDS", type=int,
                        default=0, help="Observe externally triggered DIN/AIN subscription changes")
    parser.add_argument("--modbus-write", action="store_true",
                        help="Legacy flag; --modbus-ip already runs wired I/O tests. "
                             "Use with --modbus-relay/--modbus-ao for targeted writes")
    parser.add_argument("--modbus-relay", type=int, metavar="1-8",
                        help="Relay channel for --modbus-write (physically operates relay)")
    parser.add_argument("--modbus-ao", type=int, metavar="1-4",
                        help="Analog output channel for --modbus-write")
    parser.add_argument("--modbus-ao-value", type=float, metavar="VALUE",
                        help="Deliberately selected engineering value to write to AOUT")
    parser.add_argument("--modbus-core-outage", action="store_true",
                        help="Interactive Core stop/start; verify stale-data rejection and recovery")
    parser.add_argument("--modbus-reboot", action="store_true",
                        help="Reboot the selected controller and verify Modbus feedback recovery")
    args = parser.parse_args()
    if not args.modbus and not args.modbus_ip and (args.modbus_compare or
            args.modbus_observe_seconds or args.modbus_write or
            args.modbus_reboot or args.modbus_core_outage):
        parser.error("Modbus options require --modbus or --modbus-ip")
    if args.modbus_write:
        if not args.modbus_ip:
            parser.error("--modbus-write requires --modbus-ip to select exactly one device")
    elif any(x is not None for x in (args.modbus_relay, args.modbus_ao,
                                    args.modbus_ao_value)):
        parser.error("output channel/value options require --modbus-write")
    if args.modbus_relay is not None and not 1 <= args.modbus_relay <= 8:
        parser.error("--modbus-relay must be 1 through 8")
    if args.modbus_ao is not None and not 1 <= args.modbus_ao <= 4:
        parser.error("--modbus-ao must be 1 through 4")
    if (args.modbus_ao is None) != (args.modbus_ao_value is None):
        parser.error("--modbus-ao and --modbus-ao-value must be specified together")
    if args.modbus_ao_value is not None and not math.isfinite(args.modbus_ao_value):
        parser.error("--modbus-ao-value must be finite")
    if args.modbus_observe_seconds < 0:
        parser.error("--modbus-observe-seconds must not be negative")
    if (args.modbus_observe_seconds or args.modbus_reboot or
            args.modbus_core_outage) and not args.modbus_ip:
        parser.error("observation and recovery require --modbus-ip")
    return args


def main():
    args = parse_args()
    units = discover_sigcore_units()

    if not units and not args.modbus_ip:
        print("FAIL  SigCore discovery")
        print(f"      No services found for {SERVICE_TYPE}")
        print()
        print("OVERALL: FAIL")
        sys.exit(1)

    if units:
        print("PASS  SigCore discovery")
        print(f"      Found {len(units)} unit(s)")
    else:
        print("SKIP  SigCore discovery (explicit --modbus-ip target)")
    print()

    all_results = []

    for unit in units:
        all_results.extend(test_unit(unit))

    modbus_ok = True
    if args.modbus_ip:
        modbus_ok = modbus_regression(args.modbus_ip, args.modbus_ip, args)
    elif args.modbus:
        for unit in units:
            address = unit.get("ip") or unit.get("hostname")
            if not address:
                print("FAIL  Modbus regression: discovered unit has no address")
                modbus_ok = False
                continue
            if not modbus_regression(address, address, args):
                modbus_ok = False

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
    if args.modbus or args.modbus_ip:
        print(f"Modbus regression: {'PASS' if modbus_ok else 'FAIL'}")

    basic_ok = (not all_results and bool(args.modbus_ip) or
                bool(all_results) and all(all_results))

    if basic_ok and logging_ok and modbus_ok:
        print("OVERALL: PASS")
        sys.exit(0)

    print("OVERALL: FAIL")
    sys.exit(1)


if __name__ == "__main__":
    main()
