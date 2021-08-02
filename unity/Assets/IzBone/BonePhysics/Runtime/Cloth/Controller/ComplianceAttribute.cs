using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

#if UNITY_EDITOR
using UnityEditor;
#endif



namespace IzBone.BonePhysics.Cloth.Controller {

	/**
	 * Compliance値として使用するfloatにつける属性。
	 * インスペクタ表示時に、いい感じのスライダーで設定可能にする。
	 */
	public sealed class ComplianceAttribute : PropertyAttribute {
		public ComplianceAttribute() {}

#if UNITY_EDITOR
		[CustomPropertyDrawer(typeof(ComplianceAttribute))]
		sealed class Drawer : PropertyDrawer {
			public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
				var range2Attribute = (ComplianceAttribute)attribute;

				var v = property.floatValue;
				using (var cs = new EditorGUI.ChangeCheckScope()) {
					Rect pos;
					shiftPos(out pos, ref position, 100);
					EditorGUI.PrefixLabel(pos, new GUIContent(property.displayName));

					shiftPos(out pos, ref position, 150);
					v = (-Mathf.Log10( v ) - 2) / 10;
					v = Mathf.Clamp01(v);
					v = EditorGUI.Slider(pos, v, 0f, 1f);
					if ( cs.changed ) {
						if (v <= 0.0001f) v = -10;
						v = Mathf.Pow(10, -(v*10 + 2));
						property.floatValue = v;
					}
				}
			}

			void shiftPos(out Rect pos, ref Rect totalPos, int width) {
				pos = totalPos;
				pos.width=width; totalPos.x+=width; totalPos.width-=width;
			}
		}
#endif
	}

}

