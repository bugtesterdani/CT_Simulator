from __future__ import annotations

import argparse
import sys
from pathlib import Path


def ensure_shared_device_runtime() -> None:
    shared_root = Path(__file__).resolve().parents[2] / "simtest" / "device"
    if str(shared_root) not in sys.path:
        sys.path.insert(0, str(shared_root))


ensure_shared_device_runtime()

from core.device_server import DeviceServer, create_argument_parser, load_device_model  # noqa: E402


def main() -> int:
    parser = create_argument_parser()
    args = parser.parse_args()

    model = load_device_model(args.device, args.profile)
    device_name = args.device or args.profile or "device-profile"
    server = DeviceServer(model, device_name)
    return server.run(args.pipe)


if __name__ == "__main__":
    raise SystemExit(main())
