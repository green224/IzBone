using System;
using UnityEngine;

using System.Runtime.InteropServices;
using System.Linq;

namespace IzBone.Core {

	/** ポインタアクセスが簡単にできるように配列をラップしたもの */
	public unsafe sealed class PinnedArray<T> where T:unmanaged {
		// --------------------------------------- publicメンバ -------------------------------------

		// 高速化のために、実機ではフィールドアクセスに変化させる
	#if UNITY_EDITOR
		public T[] array {get; private set;} = null;
		public T* ptr {get; private set;}
		public T* ptrEnd {get; private set;}
		public int length {get; private set;}
	#else
		public T[] array = null;
		public T* ptr;
		public T* ptrEnd;
		public int length;
	#endif

		/** 初期化処理。不要になったらnullで再初期化しておくこと */
		public void reset( T[] value ) {
			if (array!=null) _handle.Free();
			array = value;

			if (array!=null) {
				_handle = GCHandle.Alloc(array, GCHandleType.Pinned);
				length = array.Length;

				ptr = (T*)_handle.AddrOfPinnedObject();
				ptrEnd = ptr + length;
			}
		}


		// ----------------------------------- private/protected メンバ -------------------------------

		GCHandle _handle;
//		static readonly long SizeOfT;

//		static PinnedArray() {
//			SizeOfT = Marshal.SizeOf(typeof(T));
//		}
		~PinnedArray() { reset(null); }


		// --------------------------------------------------------------------------------------------
	}

}
