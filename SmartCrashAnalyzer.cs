// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Collections.Generic;
using System.Linq;

namespace RaytolfasLauncher
{
    public static class SmartCrashAnalyzer
    {
        public static string? Analyze(string? rawHint, IEnumerable<string>? logLines, string language = "ru")
        {
            string fullText = (rawHint ?? "") + "\n" + string.Join("\n", (logLines ?? Enumerable.Empty<string>()).TakeLast(50));
            if (string.IsNullOrWhiteSpace(fullText))
                return null;

            bool isRu = language.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
            bool isUk = language.StartsWith("uk", StringComparison.OrdinalIgnoreCase);

            if (fullText.Contains("OutOfMemoryError", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("Could not reserve enough space", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("Invalid maximum heap size", StringComparison.OrdinalIgnoreCase))
            {
                if (isRu)
                    return "Игре не хватило оперативной памяти (или лаунчер запросил больше RAM, чем доступно системе). Увеличьте или скорректируйте ползунок RAM в Настройках.";
                if (isUk)
                    return "Грі не вистачило оперативної пам'яті (або лаунчер запросив більше RAM, ніж доступно системі). Збільшіть або налаштуйте повзунок RAM у Налаштуваннях.";
                return "The game ran out of memory (or requested more heap than available). Please adjust the RAM slider in Settings.";
            }

            if (fullText.Contains("UnsupportedClassVersionError", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("has been compiled by a more recent version of the Java Runtime", StringComparison.OrdinalIgnoreCase))
            {
                if (isRu)
                    return "Несовместимая версия Java! Для этой версии Minecraft требуется более новая или другая версия Java (например, Java 21 для 1.20.5+ или Java 8 для 1.12.2). Проверьте профиль Java в Настройках.";
                if (isUk)
                    return "Несумісна версія Java! Для цієї версії Minecraft потрібна інша версія Java (наприклад, Java 21 для 1.20.5+ або Java 8 для 1.12.2). Перевірте профіль Java у Налаштуваннях.";
                return "Incompatible Java version! This Minecraft version requires a different Java version (e.g. Java 21 for 1.20.5+ or Java 8 for 1.12.2). Check your Java profile in Settings.";
            }

            if (fullText.Contains("UnsatisfiedLinkError", StringComparison.OrdinalIgnoreCase) &&
                (fullText.Contains("lwjgl", StringComparison.OrdinalIgnoreCase) || fullText.Contains("ELFCLASS", StringComparison.OrdinalIgnoreCase)))
            {
                if (isRu)
                    return "Ошибка загрузки библиотек LWJGL (несовместимая архитектура ОС/процессора). На ARM64 Linux требуется системный LWJGL или эмуляция.";
                if (isUk)
                    return "Помилка завантаження бібліотек LWJGL (несумісна архітектура ОС/процесора). На ARM64 Linux потрібен системний LWJGL.";
                return "Failed to load native LWJGL libraries (architecture mismatch). Ensure compatible native libraries for your OS/CPU.";
            }

            if (fullText.Contains("ModResolutionException", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("net.fabricmc.loader.impl.FormattedException", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("DuplicateModsFoundException", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("Incompatible mods found", StringComparison.OrdinalIgnoreCase))
            {
                if (isRu)
                    return "Конфликт или отсутствие зависимостей модов! Один или несколько модов в папке 'mods' несовместимы с текущей версией игры или требуют дополнительные библиотеки (например, Fabric API).";
                if (isUk)
                    return "Конфлікт або відсутність залежностей модів! Один або декілька модів у папці 'mods' несумісні з поточною версією гри або потребують додаткових бібліотек (наприклад, Fabric API).";
                return "Mod conflict or missing dependency! One or more mods in your 'mods' folder are incompatible with this game version or require additional libraries (e.g. Fabric API).";
            }

            if (fullText.Contains("org.spongepowered.asm.mixin.transformer.throwables.MixinTransformerError", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("MixinApplyError", StringComparison.OrdinalIgnoreCase))
            {
                if (isRu)
                    return "Сбой применения Mixin мода. Скорее всего, один из модов поврежден или не подходит под установленный loader.";
                if (isUk)
                    return "Збій застосування Mixin моду. Скоріш за все, один з модів пошкоджено або він не підходить під цей loader.";
                return "Mixin application failed. A mod is likely incompatible with your loader or game version.";
            }

            if (fullText.Contains("GLFW error 65542", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("Pixel format not accelerated", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("WGL:", StringComparison.OrdinalIgnoreCase))
            {
                if (isRu)
                    return "Ошибка видеодрайвера (OpenGL)! Обновите драйвер видеокарты или отключите несовместимые графические модификации.";
                if (isUk)
                    return "Помилка відеодрайвера (OpenGL)! Оновіть драйвер відеокарти або вимкніть графічні модифікації.";
                return "Graphics driver error (OpenGL)! Please update your GPU drivers or remove incompatible graphics mods.";
            }

            return null;
        }
    }
}
