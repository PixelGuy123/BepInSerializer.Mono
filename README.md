# BepInSerializer.Mono

[![GitHub Repository](https://img.shields.io/badge/GitHub-Repo-blue?logo=github)](https://github.com/PixelGuy123/BepInSerializer.Mono) [![Mono Support](https://img.shields.io/badge/Mono_Support-brown?logo=unity)](https://unity.com) [![BepInEx 5](https://img.shields.io/badge/BepInEx-5-gray?labelColor=663300)](https://github.com/BepInEx/BepInEx)

---

## ⚠️ This project is NOT affiliated with [BepInEx](https://github.com/BepInEx/BepInEx/tree/master)

This project is **not maintained by the creators of BepInEx**; the logo is merely an illustration to imply the dependency of this project on BepInEx and the functionality behind this BepInSerializer.

---

During the development of a plugin to be used with a code-injector for Unity, there are many times the developer will need to create some sort of data structure to wrap up many basic types for its customized components.

In [BepInEx](https://github.com/BepInEx), it's known that attempting to create a data structure (classes, structs) marked as _serializable_ that is _actually serialized_ is virtually impossible through Unity alone.

The main reason is due to how Unity internally works; keeping things simple, when we have a custom component with a `[Serializable]` class reference, that was injected into the engine, the field pointing to the data won't actually make it after the cloning process and, instead, point to null, which loses the previous information in the process.

**BepInSerializer** is a **universal plugin** made for **BepInEx 5** and designed with **cross-platform** — Windows, Mac OS, Linux — in mind. This project aims to fix the aforementioned issue by acting as an intermediate bridge in the serialization process that handles all the `UnityEngine.Object` instantiation calls, and properly serialize/deserialize their fields using a custom conversion system.

This plugin does **not** add any other meaningful elements to the gameplay; there isn't much else this does aside from serialization. If you ever find anything different inside the gameplay with only this plugin alone, then you're welcome to report such bug in the [Issues Tracker](https://github.com/PixelGuy123/BepInSerializer.Mono/issues).

## ❓ Why only Mono?

If you've paid enough attention, this project only has the suffix **Mono**, which means it'll work exclusively for any Unity build made **without IL2CPP.**

There are two main reasons for this to occur:

1. The project was made for **BepInEx 5**, which does not work with IL2CPP. However, a version for BepInEx 6 is still planned to be developed. Although, the second reason is...
2. Due to the fact **BepInSerializer** has been made from the ground up to attend **Mono** projects, the architecture barely understands the complexity of **IL2CPP Interop.** And so, at this point, it would be better to not even touch this type of closed compilation _yet_.

> **This project will still be maintained in the Mono environment.**
>
> If you're an ambitious developer, and you're willing to [**contribute to this project**](#-contributing) with a **IL2CPP** solution, _or even a **BepInEx 6** build_, you're always welcome to do so! We'd appreciate it! 😁

---

## 🚀 Getting Started

### For Users/Players

If you're here because a **mod** has a dependency on this project, just follow the [Installation](#-installation) guide and you're good to go.

### For Plugin Developers

- [Basics](Documentation/Basics.md#basics)
  - [Dependency Inclusion](Documentation/Basics.md#-dependency-inclusion)
  - [Basic Usage](Documentation/Basics.md#-basic-usage)
  - [Implementing `ISerializationCallbackReceiver`](Documentation/Basics.md#-integration-with-iserializationcallbackreceiver)
- [Attributes](Documentation/Attributes.md)
  - [`[UseConverters()]`](Documentation/Attributes.md#using-useconverterfieldconverter)
  - [`[AllowCollectionNesting]`](Documentation/Attributes.md#using-allowcollectionnesting)
- [Converters](Documentation/Converters.md)
  - [What is a Converter?](Documentation/Converters.md#-what-is-a-converter)
  - [Built-in Converters](Documentation/Converters.md#️-built-in-converters)
  - [How are converters chosen](Documentation/Converters.md#-how-converters-are-chosen)
- [Writing Converters](Documentation/Writing-Converters.md)
  - [Core Concepts](Documentation/Writing-Converters.md#-core-concepts)
  - [Understanding `FieldContext`](Documentation/Writing-Converters.md#-understanding-fieldcontext)
  - [What is Circular Dependency Detection?](Documentation/Writing-Converters.md#-what-is-circular-dependency-detection)
  - [Helper Methods in `FieldConverter`](Documentation/Writing-Converters.md#-helper-methods-in-fieldconverter)
  - [Writing a Custom Converter: `IEnumerable`](Documentation/Writing-Converters.md#️-writing-a-custom-converter-ienumerable)
  - [Testing Your Converter](Documentation/Writing-Converters.md#-testing-your-converter)

## 📦 Installation

> BepInSerializer is a **plugin** made for **BepInEx 5**.
>
> If you're looking for building the binary from the repository, follow the instructions in the [**Cloning & Building locally** section](#-cloning--building-locally).

Here's the step-by-step to install this plugin into your game:

1. Install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html) into the game you're wishing to play with mods.
2. Once BepInEx is installed, download this plugin through the [Releases](https://github.com/PixelGuy123/BepInSerializer.Mono/releases/latest) page.
3. With the binary downloaded, your last task is to merely put that inside the `BepInEx/plugins` folder.

Note that running the game alone with this plugin won't really change much inside the game itself.

You can verify if the serializer **loaded in** through BepInEx Console or inside `BepInEx/LogOutputs.log`.

On the other hand, in order to verify that the serializer is **working** as intended, use a separate mod that supports this serializer to assure everything's functioning as expected.

## 🍴 Cloning & Building Locally

### 📝 Requirements

- [NET Framework 4.6](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net46)

### 🖥️ Cloning from Git

This basic bash script should do the job:

```bash
git clone https://github.com/PixelGuy123/BepInSerializer.Mono.git
cd BepInSerializer.Mono
```

> **The `.csproj` contains a **`PostBuild`** event to copy the files to a few specific game locations; it is recommended to edit this event or completely remove it.**

After cloning the project, a simple `dotnet build` should be enough to test if it builds locally. Finally, just copy-paste the compiled file, located in the `bin` folder, into any game with **BepInEx 5**.

## 🎮 Supported Unity Versions

> **You can contribute by testing the Unity builds not tested yet (⭕) in the table below!**
>
> **If you find issues with these versions, the [Issues Tracker](https://github.com/PixelGuy123/BepInSerializer.Mono/issues) is always open for feedback!**

Even though **BepInSerializer** is meant to be universal and Unity barely changes their serialization/instantiation rules, there's always a bit of _unpredictability_ when working with different Unity versions.

Currently, since this project is held by a single developer, it is quite impossible to ensure _every_ Unity version is safe to use, only the newer ones ~~(from games I have installed)~~ have been tested — `2020.1.x`, `2021.1.x` and `6000.1.x`. Nevertheless, as time goes on, more Unity versions can be tested by the community or by the developer.

Here is a table of all the Unity versions this tool has seen once in its lifetime:

|Unity Sub-Version|**2020**         |**2021**         |**2022**         |**2023**         |**6000**         |
|-----------------|-----------------|-----------------|-----------------|-----------------|-----------------|
|**XXXX.0.X**     |-                |-                |-                |-                |**6000.0.x** – ⭕|
|**XXXX.1.X**     |**2020.1.x** – ✅|**2021.1.x** – ⭕|**2022.1.x** – ✅|**2023.1.x** – ⭕|**6000.1.x** – ✅|
|**XXXX.2.X**     |**2020.2.x** – ⭕|**2021.2.x** – ⭕|**2022.2.x** – ⭕|**2023.2.x** – ⭕|**6000.2.x** – ⭕|
|**XXXX.3.X**     |**2020.3.x** – ⭕|**2021.3.x** – ⭕|**2022.3.x** – ⭕|-                |**6000.3.x** – ⭕|
|**XXXX.4.X**     |-                |-                |-                |-                |**6000.4.x** – ⭕|
|**XXXX.5.X**     |-                |-                |-                |-                |**6000.5.x** – ⭕|

**Subtitles:**

- ✅ – Fully tested/supported.
- 🚧 – Tested and Planned to be supported.
- ❌ – Tested and Not planned to be supported.
- ⭕ – Not tested yet.

## 🖼️ Serialization Showcase

---

## 🤝 Contributing

Check how to contribute to this project in the [Contributing section](CONTRIBUTING.MD).

## 📜 License

This project is licensed under the **MIT** license. For more information, see [License](LICENSE).
