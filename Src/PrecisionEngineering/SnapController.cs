﻿using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using PrecisionEngineering.Utilities;
using UnityEngine;

namespace PrecisionEngineering
{
	class SnapController
	{

		private static readonly MethodInfo SnapDirectionOriginalMethodInfo = typeof (NetTool).GetMethod("SnapDirection");
		private static readonly MethodInfo SnapDirectionOverrideMethodInfo = typeof (SnapController).GetMethod("SnapDirectionOverride");

		private static RedirectCallsState _revertState;
		private static bool _hasControl;

		static SnapController()
		{
			
		}

		public static void StealControl()
		{

			if (_hasControl)
				return;


			_revertState = RedirectionHelper.RedirectCalls(SnapDirectionOriginalMethodInfo, SnapDirectionOverrideMethodInfo);
			_hasControl = true;

		}

		public static void ReturnControl()
		{

			if (!_hasControl)
				return;

			RedirectionHelper.RevertRedirect(SnapDirectionOriginalMethodInfo, _revertState);
			_hasControl = false;
		}

		public static bool EnableSnapping;

		public static NetTool.ControlPoint SnapDirectionOverride(NetTool.ControlPoint newPoint, NetTool.ControlPoint oldPoint,
			NetInfo info, out bool success, out float minDistanceSq)
		{

			if (!EnableSnapping) {

				return SnapDirectionOriginal(newPoint, oldPoint, info, out success, out minDistanceSq);

			}

			minDistanceSq = info.GetMinNodeDistance();
			minDistanceSq = minDistanceSq * minDistanceSq;
			var controlPoint = newPoint;
			success = false;

			return controlPoint;

		}

		private static NetTool.ControlPoint SnapDirectionOriginal(NetTool.ControlPoint newPoint, NetTool.ControlPoint oldPoint,
			NetInfo info, out bool success, out float minDistanceSq)
		{

			minDistanceSq = info.GetMinNodeDistance();
			minDistanceSq = minDistanceSq * minDistanceSq;
			var controlPoint = newPoint;
			success = false;
			if (oldPoint.m_node != 0) {
				var netNode = Singleton<NetManager>.instance.m_nodes.m_buffer[oldPoint.m_node];
				for (var index = 0; index < 8; ++index) {
					var segment = netNode.GetSegment(index);
					if (segment != 0) {
						var netSegment = Singleton<NetManager>.instance.m_segments.m_buffer[segment];
						var v = (int)netSegment.m_startNode != (int)oldPoint.m_node
							? netSegment.m_endDirection
							: netSegment.m_startDirection;
						v.y = 0.0f;
						if (newPoint.m_node == 0 && !newPoint.m_outside) {
							Vector3 vector3 = Line2.Offset(VectorUtils.XZ(v), VectorUtils.XZ(oldPoint.m_position - newPoint.m_position));
							var sqrMagnitude1 = vector3.sqrMagnitude;
							if (sqrMagnitude1 < (double)minDistanceSq) {
								vector3 = newPoint.m_position + vector3 - oldPoint.m_position;
								var num = (float)(vector3.x * (double)v.x + vector3.z * (double)v.z);
								controlPoint.m_position = oldPoint.m_position + v * num;
								controlPoint.m_position.y = newPoint.m_position.y;
								controlPoint.m_direction = (double)num >= 0.0 ? v : -v;
								minDistanceSq = sqrMagnitude1;
								success = true;
							}
							if (info.m_maxBuildAngle > 89.0) {
								v = new Vector3(v.z, 0.0f, -v.x);
								vector3 = Line2.Offset(VectorUtils.XZ(v), VectorUtils.XZ(oldPoint.m_position - newPoint.m_position));
								var sqrMagnitude2 = vector3.sqrMagnitude;
								if (sqrMagnitude2 < (double)minDistanceSq) {
									vector3 = newPoint.m_position + vector3 - oldPoint.m_position;
									var num = (float)(vector3.x * (double)v.x + vector3.z * (double)v.z);
									controlPoint.m_position = oldPoint.m_position + v * num;
									controlPoint.m_position.y = newPoint.m_position.y;
									controlPoint.m_direction = (double)num >= 0.0 ? v : -v;
									minDistanceSq = sqrMagnitude2;
									success = true;
								}
							}
						} else {
							var num1 = (float)(newPoint.m_direction.x * (double)v.x + newPoint.m_direction.z * (double)v.z);
							if (num1 > double.Epsilon) {
								controlPoint.m_direction = v;
								success = true;
							}
							if (num1 < -double.Epsilon) {
								controlPoint.m_direction = -v;
								success = true;
							}
							if (info.m_maxBuildAngle > 89.0) {
								v = new Vector3(v.z, 0.0f, -v.x);
								var num2 = (float)(newPoint.m_direction.x * (double)v.x + newPoint.m_direction.z * (double)v.z);
								if (num2 > double.Epsilon) {
									controlPoint.m_direction = v;
									success = true;
								}
								if (num2 < -double.Epsilon) {
									controlPoint.m_direction = -v;
									success = true;
								}
							}
						}
					}
				}
			} else if (oldPoint.m_direction.sqrMagnitude > 0.5) {
				var v = oldPoint.m_direction;
				if (newPoint.m_node == 0 && !newPoint.m_outside) {
					Vector3 vector3_1 = Line2.Offset(VectorUtils.XZ(v), VectorUtils.XZ(oldPoint.m_position - newPoint.m_position));
					var sqrMagnitude1 = vector3_1.sqrMagnitude;
					if (sqrMagnitude1 < (double)minDistanceSq) {
						var vector3_2 = newPoint.m_position + vector3_1 - oldPoint.m_position;
						var num = (float)(vector3_2.x * (double)v.x + vector3_2.z * (double)v.z);
						controlPoint.m_position = oldPoint.m_position + v * num;
						controlPoint.m_position.y = newPoint.m_position.y;
						controlPoint.m_direction = (double)num >= 0.0 ? v : -v;
						minDistanceSq = sqrMagnitude1;
						success = true;
					}
					if (info.m_maxBuildAngle > 89.0) {
						v = new Vector3(v.z, 0.0f, -v.x);
						Vector3 vector3_2 = Line2.Offset(VectorUtils.XZ(v), VectorUtils.XZ(oldPoint.m_position - newPoint.m_position));
						var sqrMagnitude2 = vector3_2.sqrMagnitude;
						if (sqrMagnitude2 < (double)minDistanceSq) {
							vector3_2 = newPoint.m_position + vector3_2 - oldPoint.m_position;
							var num = (float)(vector3_2.x * (double)v.x + vector3_2.z * (double)v.z);
							controlPoint.m_position = oldPoint.m_position + v * num;
							controlPoint.m_position.y = newPoint.m_position.y;
							controlPoint.m_direction = (double)num >= 0.0 ? v : -v;
							minDistanceSq = sqrMagnitude2;
							success = true;
						}
					}
				} else {
					var num1 = (float)(newPoint.m_direction.x * (double)v.x + newPoint.m_direction.z * (double)v.z);
					if (num1 > double.Epsilon) {
						controlPoint.m_direction = v;
						success = true;
					}
					if (num1 < -double.Epsilon) {
						controlPoint.m_direction = -v;
						success = true;
					}
					if (info.m_maxBuildAngle > 89.0) {
						v = new Vector3(v.z, 0.0f, -v.x);
						var num2 = (float)(newPoint.m_direction.x * (double)v.x + newPoint.m_direction.z * (double)v.z);
						if (num2 > double.Epsilon) {
							controlPoint.m_direction = v;
							success = true;
						}
						if (num2 < -double.Epsilon) {
							controlPoint.m_direction = -v;
							success = true;
						}
					}
				}
			}
			return controlPoint;

		}

	}
}