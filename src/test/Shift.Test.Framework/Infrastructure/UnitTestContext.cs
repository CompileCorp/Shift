using Moq;
using Moq.AutoMock;
using Xunit;

namespace Shift.Test.Framework.Infrastructure;

public abstract class UnitTestContext<T> : IDisposable
    where T : class
{
    private AutoMocker _autoMock = CreateAutoMocker();
    private T? _sut;

    public UnitTestContext()
    {
        _autoMock = CreateAutoMocker(); // Reset AutoMocker to clear mock state and avoid mock contamination between tests
        _sut = null;

        SetUp();
    }

    /// <summary>
    /// System under test
    /// </summary>
    public T Sut => _sut ??= _autoMock.CreateInstance<T>();

    /// <summary>
    /// Returns a mock for the given type.
    /// Ensures the same mock instance is returned for subsequent calls.
    /// </summary>
    public Mock<TMockType> GetMockFor<TMockType>() where TMockType : class
    {
        return _autoMock.GetMock<TMockType>();
    }

    /// <summary>
    /// Use this method when you want a real implementation of a dependency
    /// rather than a mock. Common scenarios include:
    /// - Non-mockable dependencies
    /// - Lightweight in-memory implementations
    /// - Behavior that matters for test correctness
    /// </summary>
    public void InjectConcreteClass<TClassType>(TClassType injectedClass) where TClassType : class
    {
        ArgumentNullException.ThrowIfNull(injectedClass);

        _autoMock.Use(injectedClass);
    }

    private static AutoMocker CreateAutoMocker() => new AutoMocker(MockBehavior.Loose);

    /// <summary>
    /// Override to perform additional setup after mocks/services are created.
    /// Runs once before each test (via constructor).
    /// </summary>
    public virtual void SetUp()
    {
    }

    /// <summary>
    /// Called automatically after each test
    /// </summary>
    public virtual void Dispose()
    {
        // Reset test state for next test
        _sut = null;
        _autoMock = null!;
    }
}
