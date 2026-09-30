"""Focused tests for Xcode CI preparation; uses only disposable fixtures."""
import importlib.util
import os
from pathlib import Path
import plistlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("prepare_xcode", Path(__file__).parent / "ios/scripts/prepare_xcode.py")
ci = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ci)
package_spec = importlib.util.spec_from_file_location("package_ios", Path(__file__).parent / "package_ios.py")
package = importlib.util.module_from_spec(package_spec)
package_spec.loader.exec_module(package)


class PackageCredentialTests(unittest.TestCase):
    def test_license_heading_and_rule_are_not_a_credential_assignment(self):
        package.reject_sensitive_content(b"license:\n\n----------------------------------------\n", "ios/LICENSING.txt")
        with self.assertRaises(package.PackageError):
            package.reject_sensitive_content(b"password: realCredential123456\n", "ios/config.yml")

    def test_mono_public_token_mapping_is_allowed_but_other_tokens_are_rejected(self):
        name = "ios/Data/Managed/mono/4.0/machine.config"
        mapping = b'<map Token="b77a5c561934e089" PublicKey="' + b'0123456789abcdef' * 16 + b'" />'
        package.reject_sensitive_content(mapping, name)
        with self.assertRaises(package.PackageError):
            package.reject_sensitive_content(mapping + b'\nToken="realCredential123456"', name)
        with self.assertRaises(package.PackageError):
            package.reject_sensitive_content(b'password="realCredential123456"', name)


class XcodePreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.ios = self.root / "ios"
        (self.ios / "Unity-iPhone.xcodeproj").mkdir(parents=True)
        self.project = self.ios / "Unity-iPhone.xcodeproj/project.pbxproj"
        self.project.write_text('PRODUCT_BUNDLE_IDENTIFIER = com.emberfield.prototype;\n'
                                'PRODUCT_BUNDLE_IDENTIFIER = "com.unity3d.framework";\n'
                                'CURRENT_PROJECT_VERSION = 1;\n', encoding="utf-8")
        self.plist = self.ios / "Info.plist"
        self.plist.write_bytes(plistlib.dumps({"CFBundleIdentifier": "$(PRODUCT_BUNDLE_IDENTIFIER)",
                                              "CFBundleVersion": "1", "CFBundleShortVersionString": "0.3.0"}))

    def test_application_id_and_ci_number_leave_framework_identity_intact(self):
        report = ci.prepare(self.root, {"BUNDLE_ID": "com.example.game", "BUILD_NUMBER": "4", "BUILD_NUMBER_OFFSET": "20"})
        self.assertEqual(25, report["build_number"])
        self.assertIn('"com.unity3d.framework"', self.project.read_text())
        self.assertIn('"com.example.game"', self.project.read_text())
        self.assertEqual("25", plistlib.loads(self.plist.read_bytes())["CFBundleVersion"])

    def test_lfs_pointer_blocks_before_identifier_changes(self):
        before = self.project.read_bytes()
        (self.ios / "archive.a").write_bytes(b"version https://git-lfs.github.com/spec/v1\noid sha256:abc\n")
        with self.assertRaisesRegex(ValueError, "Unresolved Git LFS"):
            ci.prepare(self.root, {"BUNDLE_ID": "com.example.game"})
        self.assertEqual(before, self.project.read_bytes())

    def test_custom_export_id_is_read_from_unity_summary(self):
        self.project.write_text(self.project.read_text().replace("com.emberfield.prototype", "com.studio.original"), encoding="utf-8")
        (self.ios / "emberfield-export-summary.txt").write_text("Result: Succeeded\nBundle: com.studio.original\n", encoding="utf-8")
        ci.prepare(self.root, {"BUNDLE_ID": "com.studio.new"})
        self.assertIn('"com.studio.new"', self.project.read_text())
        self.assertIn('"com.unity3d.framework"', self.project.read_text())

    def test_missing_application_id_fails_instead_of_rewriting_framework(self):
        self.project.write_text('PRODUCT_BUNDLE_IDENTIFIER = com.unity3d.framework;', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "framework IDs"):
            ci.prepare(self.root, {})

    def test_invalid_bundle_and_negative_build_rejected(self):
        for env in ({"BUNDLE_ID": "com.example.*"}, {"BUILD_NUMBER": "-2"}):
            with self.assertRaises(ValueError):
                ci.prepare(self.root, env)

    def test_windows_export_binaries_detected_for_executable_permissions(self):
        (self.ios / "bee_backend").write_bytes(bytes.fromhex("cffaedfe") + b"fixture")
        (self.ios / "script.sh").write_bytes(b"#!/bin/sh\r\nexit 0\r\n")
        report = ci.prepare(self.root, {})
        self.assertEqual(2, report["executable_permissions_restored"])
        self.assertNotIn(b"\r", (self.ios / "script.sh").read_bytes())


if __name__ == "__main__":
    unittest.main()
