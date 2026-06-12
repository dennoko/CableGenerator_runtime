namespace CableGeneratorRuntime
{
    /// <summary>
    /// 断面をスプライン距離に応じて回転（ねじれ）させるプロファイル用の追加インターフェース。
    /// CableProfile のサブクラスがこれを実装すると、CableGenerator が押し出し時に断面を回転させる。
    /// </summary>
    public interface ITwistedProfile
    {
        /// <summary>1メートルあたりのねじれ回転数（負値で逆方向）</summary>
        float GetTwistTurnsPerMeter();
    }
}
