using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace IzBone.Common.Field {


	/**
	 * 半減期を格納する構造体。
	 * 
	 * Burst領域にもそのまま持っていける。
	 */
	[Serializable]
	public struct HalfLife
	{
		public float value;

		public HalfLife(float val) {
			value = val;
		}

		static public implicit operator HalfLife(float val) => new HalfLife(val);

		/** t=0のとき1としたときの、指定時刻経過時の値。0～1 */
		public float evaluate(float t) => Math8.calcHL(value, t);

		/** t=0のとき1としたときの、指定時刻経過時の値を、t=0->1で積分する。結果は0～tの範囲になる */
		public float evaluateIntegral(float t) => Math8.calcIntegralHL(value, t);
	}


	/**
	 * HalfLife型を減衰力としてインスペクタ表示を行うための属性。
	 * 
	 * 属性を使わずに、直接処理を使用することがあるので、EditorOnlyにはしていない。
	 */
	internal sealed class HalfLifeDragAttribute : PropertyAttribute
	{
		public const float LEFT_VAL = 5;
		public const float RIGHT_VAL = 0.01f;

		// 減衰力として表示する値と、実際の半減期の値との相互変換
		static public float halfLife2ShowValue(float hl) =>
			(float)PowRangeAttribute.srcValue2showValue(
				hl, 1000, LEFT_VAL, RIGHT_VAL
			);
		static public float showValue2HalfLife(float val) =>
			(float)PowRangeAttribute.showValue2srcValue(
				val, 1000, LEFT_VAL, RIGHT_VAL
			);
	}


}
