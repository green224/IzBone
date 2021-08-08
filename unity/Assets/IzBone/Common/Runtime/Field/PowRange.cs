using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace IzBone.Common.Field {


	/**
	 * floatを対数グラフ的な表示でスライダー表示するための属性。
	 * ShaderのPowerSlider属性と似たような感じ
	 * 
	 * 属性を使わずに、直接処理を使用することがあるので、EditorOnlyにはしていない。
	 */
	internal sealed class PowRangeAttribute : PropertyAttribute
	{
		// 対数の底。0~1,1~∞ が設定可能範囲。0,1は特異点なので指定不可
		public readonly float baseNum;

		// 最大値・最小値
		public readonly float left, right;

		// 表示する値をpoweredValueにするか、もしくは実際の値をpoweredValueにするか
		public readonly bool isShowPowValue;

		public PowRangeAttribute(
			float baseNum, float left, float right,
			bool isShowPowValue = false
		) {
			this.baseNum = baseNum;
			this.left = left;
			this.right = right;
			this.isShowPowValue = isShowPowValue;
		}


		// 表示する値の位置(0～1)と、実際の値(left～right)との相互変換
		public float srcValue2showValue(float srcValue) =>
			(float)srcValue2showValue(srcValue, baseNum, left, right, isShowPowValue);
//			return isShowPowValue
//				? (float)linValue2powValue_withLinLR( srcValue, baseNum, left, right )
//				: (float)powValue2linValue_withPowLR( srcValue, baseNum, left, right );
		public float showValue2srcValue(float showValue) =>
			(float)showValue2srcValue(showValue, baseNum, left, right, isShowPowValue);
//			return isShowPowValue
//				? (float)powValue2linValue_withLinLR( showValue, baseNum, left, right )
//				: (float)linValue2powValue_withPowLR( showValue, baseNum, left, right );

		// 表示する値の位置(0～1)と、実際の値(left～right)との相互変換をstaticで行う処理
		static public double srcValue2showValue(
			double srcValue, double baseNum,
			double srcLeft, double srcRight, bool isShowPowValue=false
		) {
			var a = isShowPowValue
				? linValue2powValue(srcValue, baseNum)
				: powValue2linValue(srcValue, baseNum);
			var l = isShowPowValue
				? linValue2powValue(srcLeft, baseNum)
				: powValue2linValue(srcLeft, baseNum);
			var r = isShowPowValue
				? linValue2powValue(srcRight, baseNum)
				: powValue2linValue(srcRight, baseNum);

			// 表示する値は比較的平均的に分布することが期待されるので、この段階で範囲制限を掛ける
			a = (a - l) / (r - l);
			return clamp(a, 0, 1);
		}
		static public double showValue2srcValue(
			double showValue, double baseNum,
			double srcLeft, double srcRight, bool isShowPowValue=false
		) {
			var l = isShowPowValue
				? linValue2powValue(srcLeft, baseNum)
				: powValue2linValue(srcLeft, baseNum);
			var r = isShowPowValue
				? linValue2powValue(srcRight, baseNum)
				: powValue2linValue(srcRight, baseNum);

			// 表示する値は比較的平均的に分布することが期待されるので、この段階で範囲制限を解除する
			var a = clamp(showValue, 0, 1);
			a = lerp( l, r, a );

			return isShowPowValue
				? powValue2linValue(a, baseNum)
				: linValue2powValue(a, baseNum);
		}

//		// 対数変化の値(0～1)と、線形変化の値(0～1)との相互変換
//		static public float linearValue2powValue(float linearValue, float baseNum) =>
//			(float)linearValue2powValue((double)linearValue, baseNum);
//		static public float powValue2linearValue(float poweredValue, float baseNum) =>
//			(float)powValue2linearValue((double)poweredValue, baseNum);
//		static public double linearValue2powValue(double realValue, double baseNum) {
//			var a = clamp(realValue, 0, 1);
//
//			a = ( pow(baseNum, a) - 1 ) / ( baseNum - 1 );
//
//			return clamp(a, 0, 1);
//		}
//		static public double powValue2linearValue(double poweredValue, double baseNum) {
//			var a = clamp(poweredValue, 0, 1);
//
//			a = log2(a*(baseNum-1) + 1) / log2(baseNum);
//
//			return clamp(a, 0, 1);
//		}

//		// 対数変化の値(0～1)と、線形変化の値(0～1)との相互変換。
//		// 計算精度を保つために、Left/Rightをどちら側で持つかによって式を変えている。
//		static public double linValue2powValue_withPowLR(
//			double linearValue, double baseNum,
//			double poweredLeft, double poweredRight
//		) {
//			var a = pow(baseNum, linearValue);
//			a = ( a - poweredLeft ) / ( poweredRight - poweredLeft );
//			return clamp(a, 0, 1);
//		}
//		static public double linValue2powValue_withLinLR(
//			double linearValue, double baseNum,
//			double linearLeft, double linearRight
//		) => linValue2powValue_withPowLR(
//			linearValue, baseNum,
//			pow(baseNum, linearLeft), pow(baseNum, linearRight)
//		);
//		static public double powValue2linValue_withLinLR(
//			double poweredValue, double baseNum,
//			double linearLeft, double linearRight
//		) {
//			var a = log2(poweredValue) / log2(baseNum);
//			a = ( a - linearLeft ) / ( linearRight - linearLeft );
//			return clamp(a, 0, 1);
//		}
//		static public double powValue2linValue_withPowLR(
//			double poweredValue, double baseNum,
//			double poweredLeft, double poweredRight
//		) => powValue2linValue_withLinLR(
//			poweredValue, baseNum,
//			log2(poweredLeft)  / log2(baseNum),
//			log2(poweredRight) / log2(baseNum)
//		);

		// 対数変化の値(0～∞)と、線形変化の値(-∞～∞)との相互変換。
		static public double linValue2powValue(double linearValue, double baseNum) =>
			pow(baseNum, linearValue);
		static public double powValue2linValue(double poweredValue, double baseNum) =>
			log2(poweredValue) / log2(baseNum);

	}


}
