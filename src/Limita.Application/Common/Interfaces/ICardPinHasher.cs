namespace Limita.Application.Common.Interfaces;

public interface ICardPinHasher
{
    string Hash(string pin);
    bool Verify(string pin, string hash);
}
