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

        /// <summary>
        /// 1ループ（1本のワイヤー）あたりの頂点数を返す。
        /// 複数ワイヤーを持つプロファイルではオーバーライドして、
        /// ループ境界でメッシュが結合されないようにする。
        /// デフォルトは全頂点で1ループ。
        /// </summary>
        public virtual int GetVerticesPerLoop() => GetVertices().Length;

        /// <summary>
        /// 各ワイヤー（ループ）の周長を返す。
        /// UV1のアスペクト比補正に使用される。
        /// デフォルトは頂点間距離の合計から自動計算。
        /// </summary>
        public virtual float[] GetPerimeters()
        {
            var verts = GetVertices();
            int vpl = GetVerticesPerLoop();
            int loopCount = verts.Length / vpl;
            var perimeters = new float[loopCount];

            for (int loop = 0; loop < loopCount; loop++)
            {
                int baseIdx = loop * vpl;
                float p = 0f;
                for (int i = 1; i < vpl; i++)
                    p += Vector2.Distance(verts[baseIdx + i], verts[baseIdx + i - 1]);
                perimeters[loop] = p;
            }

            return perimeters;
        }
    }
}
