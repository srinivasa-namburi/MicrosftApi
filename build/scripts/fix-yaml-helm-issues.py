#!/usr/bin/env python3
"""
Fix YAML issues in Aspire-generated Helm templates.
This script addresses:
1. Keys with hyphens that need to be quoted
2. Escaped quotes in values that cause parsing issues
3. Helm template references that need index notation for hyphenated keys
"""

import os
import sys
import re
from pathlib import Path
import argparse


def fix_config_yaml_file(file_path):
    """Fix ConfigMap YAML files with hyphenated keys and template references."""
    print(f"[yaml-fix] Processing: {file_path}")

    with open(file_path, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    fixed_lines = []
    in_data_section = False

    for line in lines:
        # Check if we're entering the data section
        if line.strip() == 'data:':
            in_data_section = True
            fixed_lines.append(line)
            continue

        # Check if we're leaving the data section
        if in_data_section and line and line[0] not in ' \t':
            in_data_section = False

        # Process lines in the data section
        if in_data_section and ':' in line:
            # Split into key and value parts
            match = re.match(r'^(\s*)([^:]+):\s*(.*)$', line)
            if match:
                indent, key, value = match.groups()

                # Quote keys containing hyphens if not already quoted
                if '-' in key and not (key.startswith('"') and key.endswith('"')):
                    key = f'"{key}"'

                # Fix Helm template references for keys with hyphens
                if '{{' in value and '-' in value:
                    # Pattern to match {{ .Values.config.xxx.key-with-hyphen }}
                    pattern = r'\{\{\s*\.Values\.config\.([^}]+)\.([^}]*-[^}]*)\s*\}\}'

                    def replace_with_index(m):
                        return f'{{{{ index .Values.config.{m.group(1)} "{m.group(2)}" }}}}'

                    value = re.sub(pattern, replace_with_index, value)

                    # If the value now contains index notation, use single quotes for the outer quotes
                    if 'index .Values.config' in value and value.startswith('"') and value.endswith('"'):
                        value = "'" + value[1:-1] + "'"

                # Also handle services references with hyphens
                if '{{' in value and 'services__' in value:
                    # Pattern to match {{ .Values.config.xxx.services__key-with-hyphen }}
                    pattern = r'\{\{\s*\.Values\.config\.([^}]+)\.(services__[^}]*-[^}]*)\s*\}\}'

                    def replace_services_with_index(m):
                        return f'{{{{ index .Values.config.{m.group(1)} "{m.group(2)}" }}}}'

                    value = re.sub(pattern, replace_services_with_index, value)

                    # If the value now contains index notation, use single quotes for the outer quotes
                    if 'index .Values.config' in value and value.startswith('"') and value.endswith('"'):
                        value = "'" + value[1:-1] + "'"

                fixed_lines.append(f'{indent}{key}: {value}\n')
            else:
                fixed_lines.append(line)
        else:
            fixed_lines.append(line)

    # Write the fixed content back
    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(fixed_lines)

    print(f"[yaml-fix] Fixed: {file_path}")


def fix_values_yaml_file(file_path):
    """
    Fix values.yaml files to properly escape connection strings.
    This addresses issues with nested quotes in connection strings.
    """
    print(f"[yaml-fix] Processing values file: {file_path}")

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Fix the problematic connection string pattern
    # Replace escaped quotes within connection strings with single quotes
    # Pattern: Authentication=\"Active Directory Default\"
    # Replace with: Authentication='Active Directory Default'

    # This pattern matches connection strings with escaped quotes
    pattern = r'(Authentication=)\\"([^"]+)\\"'
    replacement = r"\1'\2'"

    fixed_content = re.sub(pattern, replacement, content)

    # Write back if changes were made
    if fixed_content != content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(fixed_content)
        print(f"[yaml-fix] Fixed connection string quotes in: {file_path}")
    else:
        print(f"[yaml-fix] No changes needed for: {file_path}")


def main():
    parser = argparse.ArgumentParser(description='Fix YAML issues in Aspire-generated Helm templates')
    parser.add_argument('directory', help='Directory containing the Aspire publish output')
    parser.add_argument('--debug', action='store_true', help='Enable debug output')

    args = parser.parse_args()

    output_dir = Path(args.directory)
    if not output_dir.exists():
        print(f"[yaml-fix] ERROR: Directory does not exist: {output_dir}")
        sys.exit(1)

    print(f"\n[yaml-fix] Fixing YAML template issues in: {output_dir}")

    # Fix values.yaml first
    values_file = output_dir / 'values.yaml'
    if values_file.exists():
        fix_values_yaml_file(values_file)

    # Find and fix all ConfigMap YAML files in templates
    templates_dir = output_dir / 'templates'
    if templates_dir.exists():
        config_files = list(templates_dir.glob('*/config.yaml'))
        print(f"[yaml-fix] Found {len(config_files)} config.yaml files to fix")

        for config_file in config_files:
            try:
                fix_config_yaml_file(config_file)
            except Exception as e:
                print(f"[yaml-fix] WARNING: Failed to fix {config_file}: {e}")
                if args.debug:
                    import traceback
                    traceback.print_exc()

    print(f"[yaml-fix] Completed fixing YAML issues in: {output_dir}")


if __name__ == '__main__':
    main()