---
name: Package Installation Issue
about: Report issues with NuGet package installation or usage
title: '[PACKAGE] '
labels: 'package, bug'
assignees: ''

---

## Package Installation Issue

### Environment Information
- **RazorX.Framework Version**: 
- **Target Framework**: (e.g., .NET 9.0)
- **Package Manager**: (NuGet Package Manager / CLI / PackageReference)
- **IDE/Editor**: (Visual Studio / VS Code / Rider)
- **Operating System**: 

### Issue Description
**What happened?**
<!-- A clear and concise description of what the bug is -->

**What did you expect to happen?**
<!-- A clear and concise description of what you expected to happen -->

### Steps to Reproduce
1. 
2. 
3. 
4. 

### Package Installation Details
**Installation Command Used:**
```bash
# Example: dotnet add package RazorX.Framework --version 1.0.0-alpha
```

**Project File (csproj) Content:**
```xml
<!-- Include relevant PackageReference entries -->
```

### Build Output/Error Messages
```
<!-- Include any error messages or build output -->
```

### File System Check
**Are the following files present in your project after installation?**
- [ ] `wwwroot/js/razorx.js`
- [ ] `wwwroot/js/razorx.js.map`
- [ ] `wwwroot/js/razorx.d.ts`

### Additional Context
<!-- Add any other context about the problem here, such as:
- Network/proxy configuration
- Custom NuGet sources
- Corporate firewall issues
- Package restore behavior
-->

### Potential Workarounds Tried
- [ ] Manual package restore (`dotnet restore`)
- [ ] Clean and rebuild (`dotnet clean && dotnet build`)
- [ ] Clear NuGet cache (`dotnet nuget locals all --clear`)
- [ ] Manual file copying from package cache