# 何时用模拟（mock）

只在**系统边界**做模拟：

- 外部 API（支付、邮件等）
- 数据库（有时，优先用测试数据库）
- 时间/随机性
- 文件系统（有时）

不要模拟：

- 你自己的类/模块
- 内部协作者
- 任何你能控制的东西

## 为可模拟性而设计

在系统边界上，把接口设计成容易模拟的样子：

**1. 用依赖注入**

把外部依赖传进来，而不是在内部创建：

```typescript
// Easy to mock
function processPayment(order, paymentClient) {
  return paymentClient.charge(order.total);
}

// Hard to mock
function processPayment(order) {
  const client = new StripeClient(process.env.STRIPE_KEY);
  return client.charge(order.total);
}
```

**2. 优先用 SDK 风格的接口，而不是通用取数函数**

为每个外部操作各写一个具体函数，而不是写一个带条件判断的通用函数：

```typescript
// GOOD: Each function is independently mockable
const api = {
  getUser: (id) => fetch(`/users/${id}`),
  getOrders: (userId) => fetch(`/users/${userId}/orders`),
  createOrder: (data) => fetch('/orders', { method: 'POST', body: data }),
};

// BAD: Mocking requires conditional logic inside the mock
const api = {
  fetch: (endpoint, options) => fetch(endpoint, options),
};
```

走 SDK 这条路意味着：
- 每个模拟只返回一种具体形状
- 测试准备阶段没有条件判断
- 一眼看清一个测试用到了哪些端点
- 每个端点各自有类型安全
