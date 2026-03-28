from __future__ import annotations

from pathlib import Path

import core.profile_device_model as profile_device_model


class SpiCat25128DeviceModel(profile_device_model.DeclarativeDeviceModel):
    """Convenience Python wrapper for the CAT25128 declarative profile."""
    def __init__(self) -> None:
        """Load the default good-profile variant for CAT25128."""
        super().__init__(str(Path(__file__).with_name("spi_cat25128_good.yaml")))
