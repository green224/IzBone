using System;
using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using static Unity.Mathematics.math;




namespace IzBone.PhysCloth.Authoring {

using Common;
using Common.Field;

[CustomPropertyDrawer(typeof(ComplianceAttribute))]
sealed class Drawer : PropertyDrawer {
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		EditorGUI.BeginProperty(position, label, property);

		using (new EditorGUIUtility8.MixedValueScope(property))
		using (var cc = new EditorGUI.ChangeCheckScope()) {

			var a = ComplianceAttribute.compliance2ShowValue( property.floatValue );
			a = EditorGUI.Slider(position, label, a, 0, 1);
			a = ComplianceAttribute.showValue2Compliance( a );

			if (cc.changed) {
				property.floatValue = a;
			}
		}

		EditorGUI.EndProperty();
	}
}

}

