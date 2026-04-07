using UnityEngine;

namespace CableGeneratorRuntime
{
    /// <summary>
    /// 断面プロファイルの抽象基底クラス。
    /// 継承して GetVertices / GetNormals / GetUCoords を実装することで断面形状を定義する。
    /// </summary>
    public abstract class CableProfile : ScriptableObject
    {
        /// <summary>断面の2D頂点座標（ローカル XY 平面）</summary>
        public abstract Vector2[] GetVertices();

        /// <summary>断面の2D法線（各頂点に対応）</summary>
        public abstract Vector2[] GetNormals();

        /// <summary>断面のU座標（各頂点に対応、0〜1）</summary>
        public abstract float[] GetUCoords();
    }
}
