"""Offline test: no server or live hardware required."""
import struct
import unittest
from test_modbusbridge import adu


class PacketLayoutTests(unittest.TestCase):
    def test_mbap_length_transaction_and_unit(self):
        raw = adu(bytes.fromhex("02 00 00 00 10"), tx_id=0xBEEF)
        self.assertEqual(raw, bytes.fromhex("BE EF 00 00 00 06 01 02 00 00 00 10"))

    def test_ieee754_high_word_first(self):
        self.assertEqual(struct.pack(">f", 1.0), bytes.fromhex("3F 80 00 00"))
        self.assertEqual(struct.pack(">f", -7.25), bytes.fromhex("C0 E8 00 00"))

    def test_register_layout(self):
        # Input register pairs 0-7: AIN 1-4; pairs 8-15: AOUT feedback 1-4.
        self.assertEqual([2 * i for i in range(4)], [0, 2, 4, 6])
        self.assertEqual([8 + 2 * i for i in range(4)], [8, 10, 12, 14])


if __name__ == "__main__":
    unittest.main()
