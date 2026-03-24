from core import DeviceServer, create_argument_parser, load_device_model


def main() -> int:
    parser = create_argument_parser()
    args = parser.parse_args()

    try:
        model = load_device_model(args.device, args.profile)
        device_name = args.device or args.profile or "device-profile"
        server = DeviceServer(model, device_name)
        return server.run(args.pipe)
    except ValueError as error:
        print(f"Error: {error}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
