
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace IzBone.IzBCollider.Core {
	using Common;

	/** コライダー１セットの最親につけるコンポーネント */
	public struct BodiesPack : IComponentData {
		public Entity first;
	}

	// 以降、１コライダーごとのEntityに対して付けるコンポーネント群
	public struct Body:IComponentData {}
	public struct Body_Next:IComponentData {public Entity value;}	// 次のコライダーへの参照
	public struct Body_ShapeType:IComponentData {public ShapeType value;}
	public struct Body_Center:IComponentData {public float3 value;}
	public struct Body_R:IComponentData {public float3 value;}
	public struct Body_Rot:IComponentData {public quaternion value;}
	public struct Body_L2W:IComponentData {public float4x4 value;}
	public struct Body_L2wClmNorm:IComponentData {public float3 value;}

	// BodyとBodyAuthoringとの橋渡し役を行うためのマネージドコンポーネント
	public sealed class Body_M2D:IComponentData {
		public BodyAuthoring bodyAuth;				//!< 生成元
	}

}
