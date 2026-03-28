from __future__ import annotations

from pathlib import Path

import core.profile_device_model as profile_device_model


class Spi93c46bDeviceModel(profile_device_model.DeclarativeDeviceModel):
    """Convenience Python wrapper for the 93C46B declarative profile."""
    def __init__(self) -> None:
        """Load the default good-profile variant for 93C46B."""
        super().__init__(str(Path(__file__).with_name("spi_93c46b_good.yaml")))
