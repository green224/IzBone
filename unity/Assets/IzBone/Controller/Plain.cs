using System;
using UnityEngine;

using System.Collections.Generic;


namespace IzBone.Controller {

	/**
	 * IzBoneを使用するオブジェクトにつけるコンポーネント。
	 * 平面的な布のようなものを再現する際に使用する
	 */
	[ExecuteInEditMode]
	[AddComponentMenu("IzBone/Plain")]
	unsafe sealed class Plain : Base {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		/** 骨の情報 */
		[Serializable] sealed class BoneInfo {
			public Transform boneTop = null;
			public int depth = 1;
			public int excludeSideChain = 0;
			public int sideChainDepth => depth - excludeSideChain;
			[SerializeField] AnimationCurve _m = null;
			[SerializeField] AnimationCurve _r = null;

			[NonSerialized] public Point point = null;

			public float getM(int idx) => Math.Max(0, _m.Evaluate( depth==1 ? 0 : ((float)idx/(depth-1)) ) );
			public float getR(int idx) => Math.Max(0, _r.Evaluate( depth==1 ? 0 : ((float)idx/(depth-1)) ) );
		}
		[SerializeField] BoneInfo[] _boneInfos = null;

		[Space]
		[UnityEngine.Serialization.FormerlySerializedAs("cmpl_direct")]
		[Compliance][SerializeField] float _cmpl_direct = 0.000000001f;		//!< Compliance値 直接接続
		[UnityEngine.Serialization.FormerlySerializedAs("cmpl_diag")]
		[Compliance][SerializeField] float _cmpl_diag = 0.0000001f;			//!< Compliance値 捻じれ用の対角線接続
		[UnityEngine.Serialization.FormerlySerializedAs("cmpl_bend")]
		[Compliance][SerializeField] float _cmpl_bend = 0.00002f;			//!< Compliance値 曲げ用の１つ飛ばし接続


		// --------------------------------------- publicメンバ -------------------------------------

		[Space]
		public Vector3 g = new Vector3(0,-1,0);			//!< 重力加速度
		[Range(0.01f,5)] public float airHL = 0.1f;		//!< 空気抵抗による半減期
		[Range(1,50)] public int iterationNum = 15;		//!< 1frame当たりの計算イテレーション回数


		// ----------------------------------- private/protected メンバ -------------------------------

		Point[] _points = null;
		Constraint[] _constraints = null;
		Core.World _world = null;

		override protected void Start() {
			base.Start();
			if (!Application.isPlaying) return;

			// 質点リストを構築
			var points = new List<Point>();
			foreach ( var i in _boneInfos ) {
				Point p = i.point = new Point() {
					idx = points.Count,
					trans = i.boneTop,
					m = i.getM(0),
					r = i.getR(0),
				};
				points.Add(p);
				if (i.depth <= 1) continue;

				int k = 1;
				for (var j=p.trans; k<i.depth; ++k) {
					j=j.GetChild(0);
					var newP = new Point() {
						idx = points.Count,
						trans = j,
						parent = p,
						m = i.getM(k),
						r = i.getR(k),
					};
					p.child = newP;
					p = newP;
					points.Add(p);
				}
			}
			_points = points.ToArray();

			// 制約リストを構築
			var constraints = new List<Constraint>();
			Action<Point, Point, int> addCstr =
				(p0, p1, type) => {
					var compliance = type==0 ? _cmpl_direct : (type==1?_cmpl_diag:_cmpl_bend);
					if (compliance < 1)
						constraints.Add( new Constraint() {
							mode = Constraint.Mode.Distance,
							srcPointIdx = p0.idx,
							dstPointIdx = p1.idx,
							compliance = compliance,
						} );
				};
			for (int i=0; i<_boneInfos.Length; ++i) {
				var bc = _boneInfos[i];
				var bl0 = (i+1<_boneInfos.Length ? _boneInfos[i+1] : null);
				var bl1 = (i+2<_boneInfos.Length ? _boneInfos[i+2] : null);
				var br0 = (0<=i-1 ? _boneInfos[i-1] : null);
				var br1 = (0<=i-2 ? _boneInfos[i-2] : null);
				var c = bc.point;
				var l0 = bl0?.point; var l1 = bl1?.point;
				var r0 = br0?.point; var r1 = br1?.point;
				var d0 = c.child;    var d1 = d0?.child;
				var ld0 = l0?.child; var ld1 = l1?.child?.child;
				var rd0 = r0?.child; var rd1 = r1?.child?.child;

				int depth=0;
				while (c!=null) {
					if (d0 !=null && isChain(4,i,depth)) addCstr(c,d0,0);
					if (d1 !=null && isChain(5,i,depth)) addCstr(c,d1,2);
					if (l0 !=null && isChain(0,i,depth)) addCstr(c,l0,0);
					if (l1 !=null && isChain(1,i,depth)) addCstr(c,l1,2);
					if (ld0!=null && isChain(2,i,depth)) addCstr(c,ld0,1);
					if (ld1!=null && isChain(3,i,depth)) addCstr(c,ld1,2);
					if (rd0!=null && isChain(6,i,depth)) addCstr(c,rd0,1);
					if (rd1!=null && isChain(7,i,depth)) addCstr(c,rd1,2);

					c=c.child;
					l0=l0?.child;
					l1=l1?.child;
					d0=d0?.child;
					d1=d1?.child;
					ld0=ld0?.child;
					ld1=ld1?.child;
					rd0=rd0?.child;
					rd1=rd1?.child;
					++depth;
				}
			}
			_constraints = constraints.ToArray();

			// シミュレーション系を作成
			_world = new Core.World();
			_world.setup( _points, _constraints );
		}

		/** dir:0上,1上x2,2右上,2右上x2,2右,2右x2,3右下,3右下x2 */
		bool isChain(int dir, int boneIdx, int depthIdx) {
			var bc = _boneInfos[boneIdx];
			var bl0 = (boneIdx+1<_boneInfos.Length ? _boneInfos[boneIdx+1] : null);
			var bl1 = (boneIdx+2<_boneInfos.Length ? _boneInfos[boneIdx+2] : null);
			var br0 = (0<=boneIdx-1 ? _boneInfos[boneIdx-1] : null);
			var br1 = (0<=boneIdx-2 ? _boneInfos[boneIdx-2] : null);
			switch (dir) {
			case 0:
			case 1:
				if (bl0==null || bl0.depth<=depthIdx || bc.sideChainDepth<=depthIdx) return false;
				if (dir==1)
				if (bl1==null || bl1.depth<=depthIdx || bl0.sideChainDepth<=depthIdx) return false;
				if (0.00000001f < (dir==0?bl0.getM(depthIdx):bl1.getM(depthIdx))) return true;
				break;
			case 2:
			case 3:
				if (bl0==null || bl0.depth<=depthIdx+1 || bc.sideChainDepth<=depthIdx+1) return false;
				if (dir==3)
				if (bl1==null || bl1.depth<=depthIdx+2 || bl0.sideChainDepth<=depthIdx+2) return false;
				if (0.00000001f < (dir==2?bl0.getM(depthIdx+1):bl1.getM(depthIdx+2))) return true;
				break;
			case 4:
			case 5:
				if (bc.depth<=depthIdx+1) return false;
				if (dir==5)
				if (bc.depth<=depthIdx+2) return false;
				if (0.00000001f < (dir==4?bc.getM(depthIdx+1):bc.getM(depthIdx+2))) return true;
				break;
			case 6:
			case 7:
				if (br0==null || br0.depth<=depthIdx+1 || br0.sideChainDepth<=depthIdx+1) return false;
				if (dir==7)
				if (br1==null || br1.depth<=depthIdx+2 || br1.sideChainDepth<=depthIdx+2) return false;
				if (0.00000001f < (dir==6?br0.getM(depthIdx+1):br1.getM(depthIdx+2))) return true;
				break;
			}
			return 0.00000001f < bc.getM(depthIdx);
		}

	#if UNITY_EDITOR
		void Update() {
			if (Application.isPlaying) return;

			if (_boneInfos == null) return;
			foreach ( var i in _boneInfos ) {
				// Depthを有効範囲に丸める
				if ( i.boneTop == null ) {
					i.depth = 0;
				} else {
					int depthMax = 1;
					for (var j=i.boneTop; j.childCount!=0; j=j.GetChild(0)) ++depthMax;

					i.depth = Mathf.Clamp(i.depth, 1, depthMax);
				}
			}
		}
	#endif
			
		void LateUpdate() {
			if (!Application.isPlaying) return;
			coreUpdate(Time.deltaTime);
		}

		override protected void OnDestroy() {
			base.OnDestroy();
			if (!Application.isPlaying) return;
			_world.release();
		}

		override protected void coreUpdate(float dt) {
			base.coreUpdate(dt);
			_world.g = g;
			_world.airHL = airHL;
			for (int i=0; i<iterationNum; ++i)
				_world.update(dt/iterationNum, iterationNum, _points, _coreColliders);
		}


	#if UNITY_EDITOR
		override protected void OnDrawGizmos() {
			base.OnDrawGizmos();
			if ( !UnityEditor.Selection.Contains( gameObject.GetInstanceID() ) ) return;

			if (_boneInfos == null) return;
			for (int i=0; i<_boneInfos.Length; ++i) {
				var b = _boneInfos[i];
				if (b.boneTop == null) continue;

				var trans = b.boneTop;
				for (int dCnt=0; dCnt!=b.depth; ++dCnt) {
					Gizmos.color = new Color(1,1,1,0.5f);
					Gizmos.DrawSphere( trans.position, b.getR(dCnt) );

					if (!Application.isPlaying) {
						Gizmos.color = new Color(1,1,0);
						if (dCnt != 0) Gizmos.DrawLine( trans.position, trans.parent.position );
						if (isChain(0,i,dCnt)) {
							var b2 = _boneInfos[i+1];
							var trans2 = b2.boneTop;
							for (int dCnt2=0; dCnt2!=dCnt; ++dCnt2) {
								if ( trans2.childCount==0 ) break; else trans2=trans2.GetChild(0);
							}
							Gizmos.DrawLine( trans.position, trans2.position );
						}
					}

					if ( trans.childCount==0 ) break; else trans=trans.GetChild(0);
				}
			}

			Gizmos.color = new Color(1,1,0);
			if (_constraints != null) foreach (var i in _constraints) {
				var p0 = _world.DEBUG_getPos( i.srcPointIdx );
				var p1 = _world.DEBUG_getPos( i.dstPointIdx );
				Gizmos.DrawLine( p0, p1 );
			}

			Gizmos.color = Color.blue;
			if (_points != null) foreach (var i in _points) {
				if (i.m < 0.000001f) continue;
				var v = _world.DEBUG_getV( i.idx );
				var p = _world.DEBUG_getPos( i.idx );
//				var p = i.trans.position;
				Gizmos.DrawLine( p, p+v*0.03f );
//				Gizmos.DrawLine( p, v );
			}
		}
	#endif

		// --------------------------------------------------------------------------------------------
	}

}

