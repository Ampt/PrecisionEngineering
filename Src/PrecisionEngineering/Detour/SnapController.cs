﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ColossalFramework.UI;
using PrecisionEngineering.Data;
using PrecisionEngineering.Data.Calculations;
using PrecisionEngineering.Utilities;
using UnityEngine;

namespace PrecisionEngineering.Detour
{
	internal class SnapController
	{

		public static bool EnableSnapping;
		public static bool EnableAdvancedSnapping;

		public static readonly object GuideLineLock = new object();

		/// <summary>
		/// The GuideLine object snapped to
		/// </summary>
		public static GuideLine? SnappedGuideLine;

		/// <summary>
		/// List of the GuideLine objects generated during the last SnapDirection call
		/// </summary>
		public static readonly IList<GuideLine> GuideLines = new List<GuideLine>();

		/// <summary>
		/// Control point that was last used for generating guide lines.
		/// </summary>
		private static NetTool.ControlPoint _cachedGuideLineControlPoint;

		public static string DebugPrint = "";

		private static readonly MethodInfo SnapDirectionOriginalMethodInfo = typeof (NetTool).GetMethod("SnapDirection");
		private static readonly MethodInfo SnapOriginalMethodInfo = typeof (NetTool).GetMethod("Snap", BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly MethodInfo SnapDirectionOverrideMethodInfo =
			typeof (SnapController).GetMethod("SnapDirectionOverride");
		private static readonly MethodInfo SnapOverrideMethodInfo = typeof (SnapController).GetMethod("SnapOverride", BindingFlags.NonPublic | BindingFlags.Static);

		private static RedirectCallsState _snapDirectionRevertState;
		private static RedirectCallsState _snapRevertState;
		private static bool _hasControl;

		static SnapController() {}

		public static void StealControl()
		{

			if (_hasControl)
				return;

			_snapDirectionRevertState = RedirectionHelper.RedirectCalls(SnapDirectionOriginalMethodInfo, SnapDirectionOverrideMethodInfo);
			_snapRevertState = RedirectionHelper.RedirectCalls(SnapOriginalMethodInfo, SnapOverrideMethodInfo);

			_hasControl = true;

		}

		public static void ReturnControl()
		{

			if (!_hasControl)
				return;

			RedirectionHelper.RevertRedirect(SnapDirectionOriginalMethodInfo, _snapDirectionRevertState);
			RedirectionHelper.RevertRedirect(SnapOriginalMethodInfo, _snapRevertState);

			_hasControl = false;

		}

		public static NetTool.ControlPoint SnapDirectionOverride(NetTool.ControlPoint newPoint, NetTool.ControlPoint oldPoint,
			NetInfo info, out bool success, out float minDistanceSq)
		{

			if (Debug.Enabled) {

				DebugPrint = string.Format("oldPoint: {0}\nnewPoint:{1}", StringUtil.ToString(oldPoint),
					StringUtil.ToString(newPoint));

			}

			GuideLines.Clear();
			SnappedGuideLine = null;

			minDistanceSq = info.GetMinNodeDistance();
			minDistanceSq = minDistanceSq * minDistanceSq;
			var controlPoint = newPoint;
			success = false;

			if (EnableSnapping) {

				// If dragging from a node
				if (oldPoint.m_node != 0 && !newPoint.m_outside) {

					// Node the road build operation is starting from
					var sourceNodeId = oldPoint.m_node;
					var sourceNode = NetManager.instance.m_nodes.m_buffer[sourceNodeId];

					// Direction and length of the line from the node to the users control point
					var userLineDirection = (newPoint.m_position - sourceNode.m_position).Flatten();
					var userLineLength = userLineDirection.magnitude;
					userLineDirection.Normalize();

					var closestSegmentId = NetNodeUtility.GetClosestSegmentId(sourceNodeId, userLineDirection);

					if (closestSegmentId > 0) {

						// Snap to angle increments originating from this closest segment

						var closestSegmentDirection = NetNodeUtility.GetSegmentExitDirection(sourceNodeId, closestSegmentId);

						var currentAngle = Vector3Extensions.Angle(closestSegmentDirection, userLineDirection, Vector3.up);

						var snappedAngle = Mathf.Round(currentAngle/Settings.SnapAngle)*Settings.SnapAngle;
						var snappedDirection = Quaternion.AngleAxis(snappedAngle, Vector3.up)*closestSegmentDirection;

						controlPoint.m_direction = snappedDirection.normalized;
						controlPoint.m_position = sourceNode.m_position + userLineLength*controlPoint.m_direction;
						controlPoint.m_position.y = newPoint.m_position.y;

						minDistanceSq = (newPoint.m_position - controlPoint.m_position).sqrMagnitude;
						success = true;

						//minDistanceSq = olpo;


					}

				} else if (oldPoint.m_segment != 0 && !newPoint.m_outside) {

					// Else if dragging from a segment

					// Segment the road build operation is starting from
					var sourceSegmentId = oldPoint.m_segment;
					var sourceSegment = NetManager.instance.m_segments.m_buffer[sourceSegmentId];

					Vector3 segmentDirection;
					Vector3 segmentPosition;

					// Direction and length of the line between control points
					var userLineDirection = (newPoint.m_position - oldPoint.m_position).Flatten();
					var userLineLength = userLineDirection.magnitude;
					userLineDirection.Normalize();

					// Get direction of the segment at the branch position
					sourceSegment.GetClosestPositionAndDirection(oldPoint.m_position, out segmentPosition, out segmentDirection);

					var currentAngle = Vector3Extensions.Angle(segmentDirection, userLineDirection, Vector3.up);

					segmentDirection = segmentDirection.Flatten().normalized;

					var snappedAngle = Mathf.Round(currentAngle/Settings.SnapAngle)*Settings.SnapAngle;
					var snappedDirection = Quaternion.AngleAxis(snappedAngle, Vector3.up)*segmentDirection;

					controlPoint.m_direction = snappedDirection.normalized;
					controlPoint.m_position = oldPoint.m_position + userLineLength*controlPoint.m_direction;
					controlPoint.m_position.y = newPoint.m_position.y;

					minDistanceSq = (newPoint.m_position - controlPoint.m_position).sqrMagnitude;

					success = true;

				} else if (oldPoint.m_direction.sqrMagnitude > 0.5f) {

					if (newPoint.m_node == 0 && !newPoint.m_outside) {

						// Let's do some snapping between control point directions

						var currentAngle = Vector3Extensions.Angle(oldPoint.m_direction, newPoint.m_direction, Vector3.up);

						var snappedAngle = Mathf.Round(currentAngle/Settings.SnapAngle)*Settings.SnapAngle;
						var snappedDirection = Quaternion.AngleAxis(snappedAngle, Vector3.up)*oldPoint.m_direction.Flatten();

						controlPoint.m_direction = snappedDirection.normalized;

						controlPoint.m_position = oldPoint.m_position +
						                          Vector3.Distance(oldPoint.m_position.Flatten(), newPoint.m_position.Flatten())*
						                          controlPoint.m_direction;

						controlPoint.m_position.y = newPoint.m_position.y;

						success = true;

					}

				} else if (oldPoint.m_segment == 0 && oldPoint.m_node == 0 && newPoint.m_segment == 0 && oldPoint.m_segment == 0) {

					// Snap to angles based from north

					var userLineDirection = (newPoint.m_position - oldPoint.m_position).Flatten();
					var userLineLength = userLineDirection.magnitude;
					userLineDirection.Normalize();

					var snapDirection = Vector3.forward;

					var currentAngle = Vector3Extensions.Angle(snapDirection, userLineDirection, Vector3.up);

					var snappedAngle = Mathf.Round(currentAngle / Settings.SnapAngle) * Settings.SnapAngle;
					var snappedDirection = Quaternion.AngleAxis(snappedAngle, Vector3.up) * snapDirection;

					controlPoint.m_direction = snappedDirection.normalized;
					controlPoint.m_position = oldPoint.m_position + userLineLength * controlPoint.m_direction;
					controlPoint.m_position.y = newPoint.m_position.y;

					minDistanceSq = (newPoint.m_position - controlPoint.m_position).sqrMagnitude;
					success = true;

				}

			} else {

				// Run the default snapping

				ReturnControl();

				controlPoint = NetTool.SnapDirection(newPoint, oldPoint, info, out success, out minDistanceSq);

				StealControl();

			}
			
			if (EnableAdvancedSnapping) {

				if (controlPoint.m_segment == 0 && controlPoint.m_node == 0) {

					controlPoint = SnapDirectionGuideLines(controlPoint, oldPoint, info, ref success, ref minDistanceSq);

				}

			}

			return controlPoint;

		}

		public static NetTool.ControlPoint SnapDirectionGuideLines(NetTool.ControlPoint newPoint, NetTool.ControlPoint oldPoint,
			NetInfo info, ref bool success, ref float minDistanceSq)
		{

			var controlPoint = newPoint;

			lock (GuideLineLock) {

				SnappedGuideLine = null;
				GuideLines.Clear();

				//if (controlPoint.m_position != _cachedGuideLineControlPoint.m_position) {

				Guides.CalculateGuideLines(info, oldPoint, controlPoint, GuideLines);

				//}

				_cachedGuideLineControlPoint = controlPoint;

				if (GuideLines.Count == 0) {

					if (Debug.Enabled)
						DebugPrint += " (No GuideLines Found)";

					return newPoint;

				}

				var minDist = float.MaxValue;
				var closestLine = GuideLines[0];

				if (GuideLines.Count > 1) {

					for (var i = 0; i < GuideLines.Count; i++) {

						var gl = GuideLines[i];
						var dist = Vector3Extensions.DistanceSquared(gl.Origin, newPoint.m_position) + gl.Distance*gl.Distance;

						if (dist < minDist) {
							closestLine = gl;
							minDist = dist;
						}

					}

				}

				if (closestLine.Distance <= Settings.GuideLinesSnapDistance + closestLine.Width) {

					minDistanceSq = closestLine.Distance*closestLine.Distance;

					if (Debug.Enabled) {
						DebugPrint += " Guide: " + closestLine.Intersect.ToString();
					}

					controlPoint.m_position = closestLine.Intersect;
					controlPoint.m_position.y = newPoint.m_position.y;
					controlPoint.m_direction = oldPoint.m_position.DirectionTo(newPoint.m_position);
					success = true;

					SnappedGuideLine = closestLine;

				}

				return controlPoint;

			}

		}

		private static void SnapOverride(NetTool tool, NetInfo info, ref Vector3 point, ref Vector3 direction, Vector3 refPoint,
			float refAngle)
		{

			//Debug.Log("snap override");

			if (FakeRoadAI.DisableLengthSnap == true)
				return;

			// Original method from dotPeek

			direction = new Vector3(Mathf.Cos(refAngle), 0.0f, Mathf.Sin(refAngle));
			Vector3 vector3_1 = direction * 8f;
			Vector3 vector3_2 = new Vector3(vector3_1.z, 0.0f, -vector3_1.x);
			if ((double)info.m_halfWidth <= 4.0) {
				refPoint.x += (float)((double)vector3_1.x * 0.5 + (double)vector3_2.x * 0.5);
				refPoint.z += (float)((double)vector3_1.z * 0.5 + (double)vector3_2.z * 0.5);
			}
			Vector2 vector2 = new Vector2(point.x - refPoint.x, point.z - refPoint.z);
			float num1 = Mathf.Round((float)(((double)vector2.x * (double)vector3_1.x + (double)vector2.y * (double)vector3_1.z) * (1.0 / 64.0)));
			float num2 = Mathf.Round((float)(((double)vector2.x * (double)vector3_2.x + (double)vector2.y * (double)vector3_2.z) * (1.0 / 64.0)));
			point.x = (float)((double)refPoint.x + (double)num1 * (double)vector3_1.x + (double)num2 * (double)vector3_2.x);
			point.z = (float)((double)refPoint.z + (double)num1 * (double)vector3_1.z + (double)num2 * (double)vector3_2.z);

		}

    }
}
