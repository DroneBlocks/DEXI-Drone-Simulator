# DEXI Drone Simulator - AI Instructions

Unity-based drone simulator for the DEXI drone platform.

## Project Overview

This is a Unity project that provides:
- 3D visualization of the DEXI drone
- Simulated downward-facing camera with ROS integration
- AprilTag grid for positioning
- Integration with PX4 SITL via ROS Bridge

## Key Files

### Camera System
- `Assets/Scripts/px4_sitl/ROSCameraPublisher.cs` - Publishes camera images and CameraInfo to ROS
- `Assets/Scripts/DownwardCamera.cs` - Downward-facing camera that follows the drone

### AprilTag System
- `Assets/Scripts/AprilTagGridGenerator.cs` - Generates grid of AprilTags in scene
- Tag size is determined by the source GameObject's Transform scale

### Drone Control
- `Assets/Scripts/DroneController.cs` - Main drone physics and control
- `Assets/Scripts/px4_sitl/DroneOdometrySubscriber.cs` - Receives odometry from PX4

### ROS Integration
- `Assets/Scripts/px4_sitl/ROSBridgeManager.cs` - WebSocket connection to ROS Bridge
- Topics published: `/cam0/image_raw/compressed`, `/cam0/camera_info`

## Camera Calibration

The camera system must have matching parameters between:
1. Unity camera FOV (set in DownwardCamera.cs or scene)
2. ROSCameraPublisher.cs focal length calculation
3. ROS AprilTag detector tag_size parameter

### Current Configuration
- Image size: 320x240
- ROSCameraPublisher assumes 38° horizontal FOV
- DownwardCamera class comment mentions 66° horizontal FOV
- Focal length calculated from FOV in ROSCameraPublisher.Start()

## Commit Guidelines

- Write clear, descriptive commit messages
- Reference issue numbers when applicable
- No AI attribution in commits

## Testing

Unity project - changes can be tested by:
1. Opening in Unity Editor
2. Verifying scripts compile
3. Running the scene and checking ROS topic output
