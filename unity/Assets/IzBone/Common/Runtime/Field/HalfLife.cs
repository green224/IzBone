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
		public const float MIN_VAL = 0.01f;
		public const float MAX_VAL = 5;

		// 減衰力として表示する値と、実際の半減期の値との相互変換
		static public float showValue2HalfLife(float val) {
			var a = clamp(val, 0, 1);

//			a = pow( a, 1f/5 );
			a = ( 1 - pow(0.001f, a) ) / 0.999f;

			return lerp( MAX_VAL, MIN_VAL, clamp(a,0,1) );
		}
		static public float halfLife2ShowValue(float hl) {
			var a = hl;
			a = 1f - (a - MIN_VAL) / (MAX_VAL - MIN_VAL);

//			a = pow(a, 5);
			a = log2(1-a) / log2(0.001f);

			return clamp(a,0,1);
		}
	}


}
