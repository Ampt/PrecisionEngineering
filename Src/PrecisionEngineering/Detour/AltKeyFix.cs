﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ColossalFramework;
using UnityEngine;

namespace PrecisionEngineering.Detour
{

	/// <summary>
	/// There appears to bug in Unity3D and Windows where holding left-alt, then ctrl, then releasing left-alt will trigger Alt-GR being pressed and won't release
	/// until you press Alt-GR itself. (On Windows Alt-Ctrl = AltGR. I'm not sure why it gets stuck)
	/// This breaks camera movement etc, so I've detoured the input methods used to disable checking for AltGR when masking alt.
	/// RAlt is still checked so it shouldn't actually be a problem.
	/// </summary>
	static class AltKeyFix
	{

		public static bool DisableLengthSnap = false;

		private static RedirectCallsState _revertState1;
		private static RedirectCallsState _revertState2;

		private static readonly MethodInfo _isPressedOriginal = typeof(SavedInputKey).GetMethod("IsPressed", new Type[] {});
		private static readonly MethodInfo _isKeyUpOriginal = typeof(SavedInputKey).GetMethod("IsKeyUp");

		private static readonly MethodInfo _isPressedReplacement = typeof(AltKeyFix).GetMethod("IsPressed");
		private static readonly MethodInfo _isKeyUpReplacement = typeof(AltKeyFix).GetMethod("IsKeyUp");

		public static void Deploy()
		{

			Debug.Log("Detouring Input Methods");

			if (_isPressedOriginal == null) {
				throw new NullReferenceException("Original IsPressed method not found");
			}

			if (_isKeyUpOriginal == null) {
				throw new NullReferenceException("Original IsKeyUp method not found");
			}

			if (_isPressedReplacement == null) {
				throw new NullReferenceException("New IsPressed method not found");
			}

			if (_isKeyUpReplacement == null) {
				throw new NullReferenceException("New IsKeyUp method not found");
			}

			_revertState1 = RedirectionHelper.RedirectCalls(_isPressedOriginal, _isPressedReplacement);
			_revertState2 = RedirectionHelper.RedirectCalls(_isKeyUpOriginal, _isKeyUpReplacement);

		}

		public static void Revert()
		{

			RedirectionHelper.RevertRedirect(_isPressedOriginal, _revertState1);
			RedirectionHelper.RevertRedirect(_isKeyUpOriginal, _revertState2);

		}

		private const int MASK_KEY = 268435455;
		private const int MASK_CONTROL = 1073741824;
		private const int MASK_SHIFT = 536870912;
		private const int MASK_ALT = 268435456;

		public static bool IsPressed(SavedInputKey @this)
		{
			int num = @this.value;
			KeyCode keyCode = (KeyCode)(num & MASK_KEY);
			return keyCode != KeyCode.None && Input.GetKey(keyCode) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) == ((num & MASK_CONTROL) != 0) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) == ((num & MASK_SHIFT) != 0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) == ((num & MASK_ALT) != 0);
		}

		public static bool IsKeyUp(SavedInputKey @this)
		{
			int num = @this.value;
			KeyCode keyCode = (KeyCode)(num & MASK_KEY);
			return keyCode != KeyCode.None && Input.GetKeyUp(keyCode) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) == ((num & MASK_CONTROL) != 0) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) == ((num & MASK_SHIFT) != 0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) == ((num & MASK_ALT) != 0);
		}

	}
}
