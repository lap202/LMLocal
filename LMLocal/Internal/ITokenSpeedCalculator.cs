namespace LMLocal.Internal
{
    internal interface ITokenSpeedCalculator
    {
        void Update(int totalTokens);
        double GetTokensPerSecond();
    }
}
