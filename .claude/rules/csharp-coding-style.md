---
description: "C# 编码风格规范，适用于所有 C# 项目"
globs: ["**/*.cs"]
alwaysApply: true
---

# C# 编码风格规范

## 命名规范
- 类名、方法名、属性名：PascalCase
- 参数名、局部变量、字段：camelCase
- 私有字段：_camelCase（下划线前缀）
- 接口名：I 前缀（IService）
- 不使用匈牙利命名法

## 代码组织
- using 语句在命名空间外
- 一个文件一个公共类型
- 按 常量 → 字段 → 属性 → 构造 → 方法 → 事件 顺序排列
- 相关方法用 #region 分组

## 异步规范
- 使用 async/await，不用 .Result 或 .Wait()
- 异步方法命名 Async 后缀
- async void 仅限于事件处理
- Task.Run 用于 CPU 密集型，非 IO

## 异常规范
- 只捕获可处理的异常
- 使用具体的异常类型（非 catch(Exception)）
- 异常时记录上下文信息
- 不吞异常，不抛出 Exception 基类

## 资源管理
- 实现 IDisposable 时使用 _disposed 标志
- 使用 using 语句管理临时资源
- CancellationTokenSource 先 Cancel 再 Dispose

## LINQ
- 链式调用每行一个操作符
- 复杂查询使用方法语法而非查询语法
- 避免在循环中使用 LINQ（性能敏感处）

## 注释
- 公共 API 使用 XML 文档注释（///）
- 内部实现使用 // 注释
- 不写显而易见注释（// 循环遍历列表）
