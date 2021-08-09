using System;
using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using static Unity.Mathematics.math;



namespace IzBone.Common.Collider {

[CanEditMultipleObjects]
[CustomEditor(typeof(IzCollider))]
sealed class IzColliderInspector : Editor
{
	void OnSceneGUI() {
		var tgt = (IzCollider)target;
		tgt.DEBUG_drawGizmos();
	}

	public override void OnInspectorGUI() {
		serializedObject.Update();

		// 形状タイプ
		var sfMode = serializedObject.FindProperty( "_mode" );
		EditorGUILayout.PropertyField( sfMode );
		var mode = (IzCollider.Mode)sfMode.enumValueIndex;

		// 中心位置オフセット
		var sfCenter = serializedObject.FindProperty( "_center" );
		EditorGUILayout.PropertyField( sfCenter );

		// 半径
		var sfR = serializedObject.FindProperty( "_r" );
		var sfRX = sfR.FindPropertyRelative("x");
		var sfRY = sfR.FindPropertyRelative("y");
		var sfRZ = sfR.FindPropertyRelative("z");
		if (mode == IzCollider.Mode.Sphere) {
			using (new EditorGUIUtility8.MixedValueScope(sfR))
			using (var check = new EditorGUI.ChangeCheckScope()) {
				var r = EditorGUILayout.FloatField( "Radius", sfRX.floatValue );
				if (check.changed) sfRX.floatValue = r;
			}
		} else if (mode == IzCollider.Mode.Capsule) {
			using (var check = new EditorGUI.ChangeCheckScope()) {
				var r = EditorGUILayout.FloatField( "Radius", sfRX.floatValue );
				var h = EditorGUILayout.FloatField( "Height", sfRY.floatValue*2 );
				if (check.changed) {
					sfRX.floatValue = r;
					sfRY.floatValue = h/2;
				}
			}
		} else if (mode == IzCollider.Mode.Box) {
			using (var check = new EditorGUI.ChangeCheckScope()) {
				var r = new Vector3(sfRX.floatValue, sfRY.floatValue, sfRZ.floatValue);
				r = EditorGUILayout.Vector3Field( "Size", r*2 );
				if (check.changed) {
					sfRX.floatValue = r.x/2;
					sfRY.floatValue = r.y/2;
					sfRZ.floatValue = r.z/2;
				}
			}
		} else if (mode == IzCollider.Mode.Plane) {
		} else { throw new InvalidProgramException(); }

		// 回転
		if (mode != IzCollider.Mode.Sphere) {
			var sfRot = serializedObject.FindProperty( "_rot" );
			using (var check = new EditorGUI.ChangeCheckScope()) {
				var rot = getPropQuaternion(sfRot);
				var euler = rot.eulerAngles;
				euler = EditorGUILayout.Vector3Field("Rotation", euler);
				if (check.changed)
					setPropQuaternion( sfRot, Quaternion.Euler(euler) );
			}
		}

		serializedObject.ApplyModifiedProperties();
	}

	Quaternion getPropQuaternion(SerializedProperty prop) =>
		new Quaternion(
			prop.FindPropertyRelative("value.x").floatValue,
			prop.FindPropertyRelative("value.y").floatValue,
			prop.FindPropertyRelative("value.z").floatValue,
			prop.FindPropertyRelative("value.w").floatValue
		);
	void setPropFloat4(SerializedProperty prop, float4 v) {
		prop.FindPropertyRelative("x").floatValue = v.x;
		prop.FindPropertyRelative("y").floatValue = v.y;
		prop.FindPropertyRelative("z").floatValue = v.z;
		prop.FindPropertyRelative("w").floatValue = v.w;
	}
	void setPropQuaternion(SerializedProperty prop, quaternion q) {
		setPropFloat4(prop.FindPropertyRelative("value"), q.value);
	}
}

}

