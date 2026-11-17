// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Content.Server._Mini.SiteBridge;
using NUnit.Framework;

namespace Content.Tests.Server._Mini;

/// <summary>
/// Мини: проверка подписи и поиск файла активации релизной сборки.
/// Пара ключей своя, тестовая, боевой ключ в тестах не используется.
/// </summary>
[TestFixture]
public sealed class MiniActivationTest
{
    private const string Payload = "{\"v\":1,\"kind\":\"mini-release\",\"issued_at\":\"2026-11-14T18:00:00Z\",\"note\":\"Mini Station release\"}";

    private ECDsa _key = null!;
    private string _publicKey = string.Empty;
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _publicKey = Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());
        _root = Path.Combine(Path.GetTempPath(), "mini-activation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        _key.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private string Sign(string payload)
    {
        var signature = _key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(signature);
    }

    [Test]
    public void ValidSignaturePasses()
    {
        Assert.That(MiniActivation.Verify(Payload, Sign(Payload), _publicKey, out var error), Is.True, error);
    }

    [Test]
    public void WrongKeyFails()
    {
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = Convert.ToBase64String(other.SignData(Encoding.UTF8.GetBytes(Payload), HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));
        Assert.That(MiniActivation.Verify(Payload, signature, _publicKey, out _), Is.False);
    }

    [Test]
    public void TamperedPayloadFails()
    {
        var signature = Sign(Payload);
        var tampered = Payload.Replace("Mini Station release", "Mini Station release!");
        Assert.That(MiniActivation.Verify(tampered, signature, _publicKey, out _), Is.False);
    }

    [Test]
    public void RejectsP1363Signature()
    {
        // Контракт требует DER; подпись в формате P1363 (r||s) не должна приниматься.
        var p1363 = Convert.ToBase64String(_key.SignData(Encoding.UTF8.GetBytes(Payload), HashAlgorithmName.SHA256));
        Assert.That(MiniActivation.Verify(Payload, p1363, _publicKey, out _), Is.False);
    }

    [Test]
    public void BrokenSignatureBase64Fails()
    {
        Assert.That(MiniActivation.Verify(Payload, "не base64", _publicKey, out _), Is.False);
    }

    [Test]
    public void WrongVersionOrKindFails()
    {
        var wrongVersion = "{\"v\":2,\"kind\":\"mini-release\"}";
        Assert.That(MiniActivation.Verify(wrongVersion, Sign(wrongVersion), _publicKey, out var versionError), Is.False);
        Assert.That(versionError, Does.Contain("версия"));

        var wrongKind = "{\"v\":1,\"kind\":\"other\"}";
        Assert.That(MiniActivation.Verify(wrongKind, Sign(wrongKind), _publicKey, out var kindError), Is.False);
        Assert.That(kindError, Does.Contain("kind"));
    }

    [Test]
    public void VerifiesActivationFile()
    {
        var path = Path.Combine(_root, MiniActivation.FileName);
        File.WriteAllText(path, "{\"payload\":\"" + EscapeJson(Payload) + "\",\"signature\":\"" + Sign(Payload) + "\"}");

        Assert.That(MiniActivation.TryVerifyFile(path, _publicKey, out var error), Is.True, error);
    }

    [Test]
    public void RejectsBrokenActivationFile()
    {
        var path = Path.Combine(_root, MiniActivation.FileName);
        File.WriteAllText(path, "{}");
        Assert.That(MiniActivation.TryVerifyFile(path, _publicKey, out _), Is.False);

        File.WriteAllText(path, "not json");
        Assert.That(MiniActivation.TryVerifyFile(path, _publicKey, out _), Is.False);

        Assert.That(MiniActivation.TryVerifyFile(Path.Combine(_root, "missing.json"), _publicKey, out _), Is.False);
    }

    [Test]
    public void FindsFileInContractOrder()
    {
        var dataDir = Path.Combine(_root, "data");
        var configDir = Path.Combine(_root, "cfg");
        var cwd = Path.Combine(_root, "cwd");
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(cwd);

        var dataFile = WriteFile(dataDir);
        var cwdFile = WriteFile(cwd);
        var configFile = WriteFile(configDir);

        var args = new[]
        {
            "--data-dir", dataDir,
            "--config-file", Path.Combine(configDir, "server_config.toml"),
        };

        // 1. data-dir раньше текущей папки и папки конфига.
        Assert.That(MiniActivation.FindFile(args, null, cwd), Is.EqualTo(dataFile));

        // 2. без data-dir побеждает текущая папка.
        File.Delete(dataFile);
        Assert.That(MiniActivation.FindFile(args, null, cwd), Is.EqualTo(cwdFile));

        // 3. и только потом папка конфига.
        File.Delete(cwdFile);
        Assert.That(MiniActivation.FindFile(args, null, cwd), Is.EqualTo(configFile));

        // 4. аргумент --mini-activation важнее всего.
        var explicitFile = Path.Combine(_root, "explicit.json");
        File.WriteAllText(explicitFile, "{}");
        var explicitArgs = new[]
        {
            "--mini-activation", explicitFile,
            "--data-dir", dataDir,
            "--config-file", Path.Combine(configDir, "server_config.toml"),
        };
        Assert.That(MiniActivation.FindFile(explicitArgs, null, cwd), Is.EqualTo(explicitFile));

        // 5. переменная окружения важнее data-dir.
        var envFile = Path.Combine(_root, "env.json");
        File.WriteAllText(envFile, "{}");
        File.WriteAllText(configFile, "{}");
        Assert.That(MiniActivation.FindFile(args, envFile, cwd), Is.EqualTo(envFile));

        // 6. аргумент в форме --mini-activation=путь тоже понимается.
        var equalsArgs = new[] { "--mini-activation=" + explicitFile };
        Assert.That(MiniActivation.FindFile(equalsArgs, null, cwd), Is.EqualTo(explicitFile));

        // 7. ничего не найдено.
        Assert.That(MiniActivation.FindFile(Array.Empty<string>(), null, Path.Combine(_root, "empty")), Is.Null);
    }

    /// <summary>
    /// Блокировка с сайта: файл рядом с активацией (или в cwd), и мост пишет
    /// его же, поэтому проверка при старте находит блокировку после рестарта.
    /// </summary>
    [Test]
    public void BlockFileSitsNextToActivationOrCwd()
    {
        var cwd = Path.Combine(_root, "cwd");
        Directory.CreateDirectory(cwd);
        var activation = WriteFile(_root);

        var next = MiniActivation.BlockPath(activation, cwd);
        Assert.That(next, Is.EqualTo(Path.Combine(_root, MiniActivation.BlockFileName)));
        Assert.That(MiniActivation.IsBlocked(next), Is.False);

        File.WriteAllText(next, "выключен с сайта");
        Assert.That(MiniActivation.IsBlocked(next), Is.True);

        // Файла активации нет: блокировка в рабочей папке.
        var fallback = MiniActivation.BlockPath(null, cwd);
        Assert.That(fallback, Is.EqualTo(Path.Combine(cwd, MiniActivation.BlockFileName)));
    }

    private string WriteFile(string dir)
    {
        var path = Path.Combine(dir, MiniActivation.FileName);
        File.WriteAllText(path, "{}");
        return path;
    }

    private static string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}