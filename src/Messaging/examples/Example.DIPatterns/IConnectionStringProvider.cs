namespace Femur.Messaging.Example.DIPatterns;

public interface IConnectionStringProvider
{
    string GetConnectionString(string name);
}
