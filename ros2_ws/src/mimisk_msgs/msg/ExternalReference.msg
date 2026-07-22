std_msgs/Header header

# Target: drone, minirov, or tether.
string target_agent

# If false, Unity ignores this reference.
bool enabled

# Reference validity flags.
bool position_valid
bool velocity_valid
bool acceleration_valid
bool yaw_valid
bool depth_valid

# ROS-frame reference:
# ROS X = Unity X
# ROS Y = Unity Z
# ROS Z = Unity Y
geometry_msgs/Vector3 position
geometry_msgs/Vector3 velocity
geometry_msgs/Vector3 acceleration

# Heading about ROS Z / Unity Y.
float64 yaw_deg

# MiniROV positive-down depth reference.
float64 depth_m

# Tether target length reference.
float64 target_length_m

# Optional speed limit for future controllers.
float64 max_speed_m_s

# Optional mode string:
# drone: external_pose, external_velocity, hold
# minirov: goto, hold
# tether: target_length
string mode
