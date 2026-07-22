from setuptools import setup, find_packages

package_name = "mimisk_grpc_adapter"

setup(
    name=package_name,
    version="0.1.0",
    packages=find_packages(),
    data_files=[
        ("share/ament_index/resource_index/packages", ["resource/" + package_name]),
        ("share/" + package_name, ["package.xml"]),
    ],
    install_requires=[
        "setuptools",
        "grpcio",
    ],
    zip_safe=True,
    maintainer="Waseem",
    maintainer_email="waseem@example.com",
    description="MIMISK Unity gRPC to ROS2 adapter.",
    license="Apache-2.0",
    entry_points={
        "console_scripts": [
            "mimisk_grpc_server = mimisk_grpc_adapter.server:main",
            "mimisk_inputs_gamepad_mapper = mimisk_grpc_adapter.inputs_gamepad_mapper:main",
            "mimisk_integrated_experiment = mimisk_grpc_adapter.experiment_runner:main",
        ],
    },
)
