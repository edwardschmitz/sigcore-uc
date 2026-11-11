"""Socket-level integration tests for SIMULATED ModbusBridge ONLY.

Usage from software/ModbusBridge:
  dotnet run -- --config config/modbusbridge.simulated.json
  python3 -m unittest discover -s tests -v

NEVER run these against a live SigCore, because tests write relay and AO commands.
"""
import socket
import struct
import unittest

HOST = "127.0.0.1"
PORT = 1502
UNIT_ID = 1


def adu(pdu, tx_id=0x1234, unit_id=UNIT_ID):
    return struct.pack(">HHHB", tx_id, 0, len(pdu) + 1, unit_id) + pdu


def read_exact(sock, count):
    data = b""
    while len(data) < count:
        part = sock.recv(count - len(data))
        if not part:
            raise ConnectionError("Connection ended while reading reply")
        data += part
    return data


def transaction(sock, pdu, tx_id=0x1234, fragmented=False):
    payload = adu(pdu, tx_id=tx_id)
    if fragmented:
        for piece in (payload[:2], payload[2:6], payload[6:9], payload[9:]):
            if piece:
                sock.sendall(piece)
    else:
        sock.sendall(payload)
    header = read_exact(sock, 7)
    rx_id, protocol, length, unit = struct.unpack(">HHHB", header)
    assert (rx_id, protocol, unit) == (tx_id, 0, UNIT_ID)
    return read_exact(sock, length - 1)


class TestModbusBridge(unittest.TestCase):
    def setUp(self):
        self.sock = socket.create_connection((HOST, PORT), timeout=3)
        self.sock.settimeout(3)

    def tearDown(self):
        self.sock.close()

    def test_relay_command_and_separate_feedback(self):
        # Coil address 0 is a command, not a readable feedback bit.
        self.assertEqual(transaction(self.sock, bytes.fromhex("05 00 00 FF 00")),
                         bytes.fromhex("05 00 00 FF 00"))
        self.assertEqual(transaction(self.sock, bytes.fromhex("02 00 08 00 08")),
                         bytes.fromhex("02 01 01"))
        self.assertEqual(transaction(self.sock, bytes.fromhex("01 00 00 00 08")),
                         bytes.fromhex("81 01"))
        self.assertEqual(transaction(self.sock, bytes.fromhex("05 00 00 00 00")),
                         bytes.fromhex("05 00 00 00 00"))
        self.assertEqual(transaction(self.sock, bytes.fromhex("02 00 08 00 08")),
                         bytes.fromhex("02 01 00"))

    def test_digital_input_and_relay_feedback_share_discrete_space(self):
        self.assertEqual(transaction(self.sock, bytes.fromhex("02 00 00 00 10")),
                         bytes.fromhex("02 02 00 00"))
        self.assertEqual(transaction(self.sock, bytes.fromhex("02 00 07 00 02")),
                         bytes.fromhex("02 01 00"))

    def test_analog_inputs_read_only_float(self):
        self.assertEqual(transaction(self.sock, bytes.fromhex("04 00 00 00 08")),
                         b"\x04\x10" + b"\x00" * 16)

    def test_analog_output_command_and_separate_feedback(self):
        value = 12.375
        payload = struct.pack(">f", value)
        self.assertEqual(transaction(self.sock, b"\x10\x00\x02\x00\x02\x04" + payload),
                         bytes.fromhex("10 00 02 00 02"))
        # AO2 feedback sits at input registers 10-11, not holding registers 2-3.
        self.assertEqual(transaction(self.sock, bytes.fromhex("04 00 0A 00 02")),
                         b"\x04\x04" + payload)
        self.assertEqual(transaction(self.sock, bytes.fromhex("03 00 02 00 02")),
                         bytes.fromhex("83 01"))

    def test_register_alignment_and_partial_feedback_reads(self):
        payload = struct.pack(">f", -7.25)
        transaction(self.sock, b"\x10\x00\x06\x00\x02\x04" + payload)
        hi, lo = struct.unpack(">HH", payload)
        self.assertEqual(transaction(self.sock, bytes.fromhex("04 00 0E 00 01")),
                         b"\x04\x02" + struct.pack(">H", hi))
        self.assertEqual(transaction(self.sock, bytes.fromhex("04 00 0F 00 01")),
                         b"\x04\x02" + struct.pack(">H", lo))
        self.assertEqual(transaction(self.sock, bytes.fromhex("04 00 07 00 02")),
                         b"\x04\x04" + bytes.fromhex("00 00 00 00"))

    def test_protocol_exceptions(self):
        cases = (
            ("02 00 0F 00 02", "82 02"),
            ("02 00 00 00 00", "82 03"),
            ("05 00 08 FF 00", "85 02"),
            ("05 00 00 00 01", "85 03"),
            ("06 00 00 00 01", "86 01"),
            ("10 00 01 00 02 04 3F 80 00 00", "90 02"),
            ("10 00 00 00 02 04 7F C0 00 00", "90 03"),
            ("10 00 00 00 01 02 12 34", "90 03"),
            ("04 00 0F 00 02", "84 02"),
        )
        for request, expected in cases:
            with self.subTest(request=request):
                self.assertEqual(transaction(self.sock, bytes.fromhex(request)),
                                 bytes.fromhex(expected))

    def test_fragmented_packet_and_transaction_id_echo(self):
        response = transaction(self.sock, bytes.fromhex("02 00 00 00 10"),
                               tx_id=0xCAFE, fragmented=True)
        self.assertEqual(response[:2], bytes.fromhex("02 02"))


if __name__ == "__main__":
    unittest.main()
