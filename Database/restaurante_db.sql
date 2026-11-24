IF DB_ID('restaurante_db') IS NULL
    CREATE DATABASE restaurante_db;
GO

USE restaurante_db;
GO

IF OBJECT_ID('dbo.usuario') IS NULL
CREATE TABLE usuario (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  nombre NVARCHAR(100) NOT NULL,
  apellido NVARCHAR(100) NOT NULL,
  email NVARCHAR(150) NOT NULL UNIQUE,
  password NVARCHAR(255) NOT NULL,
  rol NVARCHAR(20) NOT NULL DEFAULT 'MESERO',
  activo BIT NOT NULL DEFAULT 1,
  created_at DATETIME2 DEFAULT GETDATE(),
  updated_at DATETIME2 DEFAULT GETDATE(),
  CONSTRAINT CK_usuario_rol CHECK (rol IN ('ADMIN', 'MESERO', 'COCINERO', 'CAJERO'))
);
GO

IF OBJECT_ID('dbo.categoria') IS NULL
CREATE TABLE categoria (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  nombre NVARCHAR(100) NOT NULL UNIQUE,
  descripcion NVARCHAR(255),
  activa BIT NOT NULL DEFAULT 1,
  created_at DATETIME2 DEFAULT GETDATE()
);
GO

IF OBJECT_ID('dbo.plato') IS NULL
CREATE TABLE plato (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  nombre NVARCHAR(150) NOT NULL,
  descripcion NVARCHAR(MAX),
  precio DECIMAL(10,2) NOT NULL,
  disponible BIT NOT NULL DEFAULT 1,
  categoria_id BIGINT NOT NULL,
  created_at DATETIME2 DEFAULT GETDATE(),
  updated_at DATETIME2 DEFAULT GETDATE(),
  CONSTRAINT FK_plato_categoria FOREIGN KEY (categoria_id) REFERENCES categoria(id),
  CONSTRAINT CK_plato_precio CHECK (precio > 0)
);
GO

IF OBJECT_ID('dbo.mesa') IS NULL
CREATE TABLE mesa (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  numero INT NOT NULL UNIQUE,
  capacidad INT NOT NULL,
  ubicacion NVARCHAR(100),
  estado NVARCHAR(20) DEFAULT 'DISPONIBLE',
  created_at DATETIME2 DEFAULT GETDATE(),
  CONSTRAINT CK_mesa_capacidad CHECK (capacidad > 0),
  CONSTRAINT CK_mesa_estado CHECK (estado IN ('DISPONIBLE', 'OCUPADA', 'RESERVADA'))
);
GO

IF OBJECT_ID('dbo.pedido') IS NULL
CREATE TABLE pedido (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  mesa_id BIGINT NOT NULL,
  mesero_id BIGINT NOT NULL,
  estado NVARCHAR(20) DEFAULT 'PENDIENTE',
  total DECIMAL(10,2) DEFAULT 0.00,
  observaciones NVARCHAR(MAX),
  fecha_creacion DATETIME2 DEFAULT GETDATE(),
  fecha_cierre DATETIME2,
  CONSTRAINT FK_pedido_mesa FOREIGN KEY (mesa_id) REFERENCES mesa(id),
  CONSTRAINT FK_pedido_mesero FOREIGN KEY (mesero_id) REFERENCES usuario(id),
  CONSTRAINT CK_pedido_estado CHECK (estado IN ('PENDIENTE', 'EN_PREPARACION', 'LISTO', 'ENTREGADO', 'PAGADO', 'CANCELADO'))
);
GO

IF OBJECT_ID('dbo.detalle_pedido', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.pedido_plato', 'U') IS NULL
    EXEC sp_rename 'dbo.detalle_pedido', 'pedido_plato';
GO

IF OBJECT_ID('dbo.pedido_plato', 'U') IS NULL
CREATE TABLE pedido_plato (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  pedido_id BIGINT NOT NULL,
  plato_id BIGINT NOT NULL,
  cantidad INT NOT NULL,
  precio_unitario DECIMAL(10,2) NOT NULL,
  subtotal DECIMAL(10,2) NOT NULL,
  notas NVARCHAR(255),
  CONSTRAINT FK_pedido_plato_pedido FOREIGN KEY (pedido_id) REFERENCES pedido(id) ON DELETE CASCADE,
  CONSTRAINT FK_pedido_plato_plato FOREIGN KEY (plato_id) REFERENCES plato(id),
  CONSTRAINT CK_pedido_plato_cantidad CHECK (cantidad > 0)
);
GO

IF COL_LENGTH('dbo.plato', 'imagen') IS NULL
    ALTER TABLE plato ADD imagen NVARCHAR(255) NULL;
GO

IF OBJECT_ID('dbo.pago') IS NULL
CREATE TABLE pago (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  mesa_id BIGINT NOT NULL,
  cajero_id BIGINT NOT NULL,
  subtotal DECIMAL(10,2) NOT NULL,
  igv DECIMAL(10,2) NOT NULL,
  total DECIMAL(10,2) NOT NULL,
  metodo NVARCHAR(20) NOT NULL,
  monto_recibido DECIMAL(10,2) NOT NULL,
  vuelto DECIMAL(10,2) NOT NULL DEFAULT 0,
  fecha DATETIME2 DEFAULT GETDATE(),
  CONSTRAINT FK_pago_mesa FOREIGN KEY (mesa_id) REFERENCES mesa(id),
  CONSTRAINT FK_pago_cajero FOREIGN KEY (cajero_id) REFERENCES usuario(id),
  CONSTRAINT CK_pago_metodo CHECK (metodo IN ('EFECTIVO', 'TARJETA', 'YAPE_PLIN', 'MIXTO')),
  CONSTRAINT CK_pago_total CHECK (total > 0 AND monto_recibido >= total)
);
GO

IF OBJECT_ID('dbo.pago_detalle') IS NULL
CREATE TABLE pago_detalle (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  pago_id BIGINT NOT NULL,
  metodo NVARCHAR(20) NOT NULL,
  importe DECIMAL(10,2) NOT NULL,
  CONSTRAINT FK_pago_detalle_pago FOREIGN KEY (pago_id) REFERENCES pago(id),
  CONSTRAINT CK_pago_detalle_metodo CHECK (metodo IN ('EFECTIVO','TARJETA','YAPE_PLIN')),
  CONSTRAINT CK_pago_detalle_importe CHECK (importe >= 0)
);
GO

IF COL_LENGTH('dbo.pedido', 'pago_id') IS NULL
    ALTER TABLE pedido ADD pago_id BIGINT NULL;
GO

IF OBJECT_ID('dbo.FK_pedido_pago') IS NULL
    ALTER TABLE pedido ADD CONSTRAINT FK_pedido_pago FOREIGN KEY (pago_id) REFERENCES pago(id);
GO

IF OBJECT_ID('dbo.insumo') IS NULL
CREATE TABLE insumo (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  nombre NVARCHAR(150) NOT NULL UNIQUE,
  unidad NVARCHAR(20) NOT NULL,
  stock_actual DECIMAL(10,2) NOT NULL DEFAULT 0,
  stock_minimo DECIMAL(10,2) NOT NULL DEFAULT 0,
  activo BIT NOT NULL DEFAULT 1,
  created_at DATETIME2 DEFAULT GETDATE(),
  CONSTRAINT CK_insumo_stock CHECK (stock_actual >= 0 AND stock_minimo >= 0)
);
GO

IF COL_LENGTH('dbo.insumo', 'costo_unitario') IS NULL
    ALTER TABLE dbo.insumo ADD costo_unitario DECIMAL(10,2) NOT NULL CONSTRAINT DF_insumo_costo DEFAULT 0;
GO

IF OBJECT_ID('dbo.plato_insumo') IS NULL
CREATE TABLE plato_insumo (
  plato_id BIGINT NOT NULL,
  insumo_id BIGINT NOT NULL,
  cantidad DECIMAL(10,3) NOT NULL,
  PRIMARY KEY (plato_id, insumo_id),
  CONSTRAINT FK_pi_plato FOREIGN KEY (plato_id) REFERENCES plato(id) ON DELETE CASCADE,
  CONSTRAINT FK_pi_insumo FOREIGN KEY (insumo_id) REFERENCES insumo(id),
  CONSTRAINT CK_pi_cantidad CHECK (cantidad > 0)
);
GO

IF OBJECT_ID('dbo.movimiento_inventario') IS NULL
CREATE TABLE movimiento_inventario (
  id BIGINT IDENTITY(1,1) PRIMARY KEY,
  insumo_id BIGINT NOT NULL,
  tipo NVARCHAR(10) NOT NULL,
  cantidad DECIMAL(10,2) NOT NULL,
  usuario_id BIGINT NOT NULL,
  insumo_nombre NVARCHAR(150) NOT NULL CONSTRAINT DF_mov_inv_nombre DEFAULT '',
  stock_anterior DECIMAL(10,2) NOT NULL CONSTRAINT DF_mov_inv_anterior DEFAULT 0,
  stock_nuevo DECIMAL(10,2) NOT NULL CONSTRAINT DF_mov_inv_nuevo DEFAULT 0,
  usuario_nombre NVARCHAR(150) NOT NULL CONSTRAINT DF_mov_inv_usuario DEFAULT '',
  fecha DATETIME2 DEFAULT GETDATE(),
  motivo NVARCHAR(255),
  CONSTRAINT FK_mi_insumo FOREIGN KEY (insumo_id) REFERENCES insumo(id),
  CONSTRAINT FK_mi_usuario FOREIGN KEY (usuario_id) REFERENCES usuario(id),
  CONSTRAINT CK_mi_tipo CHECK (tipo IN ('ENTRADA', 'SALIDA', 'AJUSTE')),
  CONSTRAINT CK_mi_cantidad CHECK (cantidad > 0)
);
GO

IF COL_LENGTH('dbo.movimiento_inventario', 'insumo_nombre') IS NULL
    ALTER TABLE dbo.movimiento_inventario ADD insumo_nombre NVARCHAR(150) NOT NULL CONSTRAINT DF_mov_inv_nombre_mig DEFAULT '';
IF COL_LENGTH('dbo.movimiento_inventario', 'stock_anterior') IS NULL
    ALTER TABLE dbo.movimiento_inventario ADD stock_anterior DECIMAL(10,2) NOT NULL CONSTRAINT DF_mov_inv_anterior_mig DEFAULT 0;
IF COL_LENGTH('dbo.movimiento_inventario', 'stock_nuevo') IS NULL
    ALTER TABLE dbo.movimiento_inventario ADD stock_nuevo DECIMAL(10,2) NOT NULL CONSTRAINT DF_mov_inv_nuevo_mig DEFAULT 0;
IF COL_LENGTH('dbo.movimiento_inventario', 'usuario_nombre') IS NULL
    ALTER TABLE dbo.movimiento_inventario ADD usuario_nombre NVARCHAR(150) NOT NULL CONSTRAINT DF_mov_inv_usuario_mig DEFAULT '';
GO

IF OBJECT_ID('dbo.caja_sesion') IS NULL
CREATE TABLE caja_sesion (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    cajero_id BIGINT NOT NULL,
    fecha_apertura DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    fecha_cierre DATETIME2 NULL,
    monto_inicial DECIMAL(10,2) NOT NULL,
    ventas_efectivo DECIMAL(10,2) NOT NULL DEFAULT 0,
    ventas_tarjeta DECIMAL(10,2) NOT NULL DEFAULT 0,
    ventas_digital DECIMAL(10,2) NOT NULL DEFAULT 0,
    egresos DECIMAL(10,2) NOT NULL DEFAULT 0,
    efectivo_contado DECIMAL(10,2) NULL,
    diferencia DECIMAL(10,2) NULL,
    observaciones NVARCHAR(500) NULL,
    estado NVARCHAR(15) NOT NULL DEFAULT 'ABIERTA',
    CONSTRAINT FK_caja_sesion_usuario FOREIGN KEY (cajero_id) REFERENCES usuario(id),
    CONSTRAINT CK_caja_sesion_estado CHECK (estado IN ('ABIERTA','CERRADA'))
);
GO

IF OBJECT_ID('dbo.movimiento_caja') IS NULL
CREATE TABLE movimiento_caja (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    sesion_id BIGINT NOT NULL,
    usuario_id BIGINT NOT NULL,
    tipo NVARCHAR(10) NOT NULL,
    concepto NVARCHAR(200) NOT NULL,
    importe DECIMAL(10,2) NOT NULL,
    fecha DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_mov_caja_sesion FOREIGN KEY (sesion_id) REFERENCES caja_sesion(id),
    CONSTRAINT FK_mov_caja_usuario FOREIGN KEY (usuario_id) REFERENCES usuario(id),
    CONSTRAINT CK_mov_caja_tipo CHECK (tipo IN ('INGRESO','EGRESO')),
    CONSTRAINT CK_mov_caja_importe CHECK (importe > 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM usuario)
INSERT INTO usuario (nombre, apellido, email, password, rol) VALUES
('Administrador', 'Sistema', 'admin@elbuensabor.pe', 'PBKDF2$100000$e+PQ8QykfQ6eu/uOy0XDEQ==$aQO8AkHzWJTIq8gBsNT+60XzUnur0ovmEOb5Xc44S9A=', 'ADMIN'),
('Juan', 'Perez', 'juan@elbuensabor.pe', 'PBKDF2$100000$OQX6COQZiEyAmmR9GWBpZQ==$oNxNpnvPoF4+Hkwpr7nT+0lGEkavD39XcNsd6AHVyFg=', 'MESERO'),
('Maria', 'Lopez', 'maria@elbuensabor.pe', 'PBKDF2$100000$yDE3Pcg31OHgW98IoLiTMA==$uS+mxPm5ptj9Y6hzaCHvdG1WAftyC6HAa/qirwX7RC0=', 'MESERO'),
('Carlos', 'Ramirez', 'carlos@elbuensabor.pe', 'PBKDF2$100000$zxD8sm3GoNJs+T65qwNPfA==$rbdxxMiJufokJSMnRszrv33vQ3hn4/+IZsV8/qfdlTg=', 'COCINERO'),
('Ana', 'Torres', 'ana@elbuensabor.pe', 'PBKDF2$100000$UQVsSfahl0Ie8Z4hycUKcA==$KQE1XtS8sFNlYorG4MxwH4WjRO5D97gjFUpz6rQjyT8=', 'CAJERO');
GO
UPDATE usuario SET password = 'PBKDF2$100000$e+PQ8QykfQ6eu/uOy0XDEQ==$aQO8AkHzWJTIq8gBsNT+60XzUnur0ovmEOb5Xc44S9A=' WHERE email = 'admin@elbuensabor.pe' AND password NOT LIKE 'PBKDF2$%';
UPDATE usuario SET password = 'PBKDF2$100000$OQX6COQZiEyAmmR9GWBpZQ==$oNxNpnvPoF4+Hkwpr7nT+0lGEkavD39XcNsd6AHVyFg=' WHERE email = 'juan@elbuensabor.pe' AND password NOT LIKE 'PBKDF2$%';
UPDATE usuario SET password = 'PBKDF2$100000$yDE3Pcg31OHgW98IoLiTMA==$uS+mxPm5ptj9Y6hzaCHvdG1WAftyC6HAa/qirwX7RC0=' WHERE email = 'maria@elbuensabor.pe' AND password NOT LIKE 'PBKDF2$%';
UPDATE usuario SET password = 'PBKDF2$100000$zxD8sm3GoNJs+T65qwNPfA==$rbdxxMiJufokJSMnRszrv33vQ3hn4/+IZsV8/qfdlTg=' WHERE email = 'carlos@elbuensabor.pe' AND password NOT LIKE 'PBKDF2$%';
UPDATE usuario SET password = 'PBKDF2$100000$UQVsSfahl0Ie8Z4hycUKcA==$KQE1XtS8sFNlYorG4MxwH4WjRO5D97gjFUpz6rQjyT8=' WHERE email = 'ana@elbuensabor.pe' AND password NOT LIKE 'PBKDF2$%';
GO

IF NOT EXISTS (SELECT 1 FROM categoria)
INSERT INTO categoria (nombre, descripcion) VALUES
('Entradas', 'Platos de entrada'),
('Platos de Fondo', 'Platos principales'),
('Bebidas', 'Jugos y refrescos'),
('Postres', 'Dulces y postres');
GO

IF NOT EXISTS (SELECT 1 FROM plato)
INSERT INTO plato (nombre, descripcion, precio, disponible, categoria_id) VALUES
('Papa a la Huancaina', 'Papa con crema de aji', 18.00, 1, 1),
('Ceviche Clasico', 'Pescado con limon y cebolla', 38.00, 1, 1),
('Lomo Saltado', 'Carne con papas y arroz', 35.00, 1, 2),
('Aji de Gallina', 'Pollo en crema de aji', 28.00, 1, 2),
('Arroz con Mariscos', 'Arroz con mariscos variados', 42.00, 1, 2),
('Chicha Morada', 'Bebida de maiz morado', 8.00, 1, 3),
('Limonada', 'Bebida de limon natural', 7.00, 1, 3),
('Suspiro a la Limena', 'Postre de manjar y merengue', 15.00, 1, 4);
GO

IF NOT EXISTS (SELECT 1 FROM mesa)
INSERT INTO mesa (numero, capacidad, ubicacion, estado) VALUES
(1, 4, 'Salon principal', 'DISPONIBLE'),
(2, 4, 'Salon principal', 'DISPONIBLE'),
(3, 2, 'Salon principal', 'DISPONIBLE'),
(4, 6, 'Salon principal', 'DISPONIBLE'),
(5, 2, 'Salon principal', 'DISPONIBLE'),
(6, 4, 'Salon principal', 'DISPONIBLE'),
(7, 4, 'Terraza', 'DISPONIBLE'),
(8, 2, 'Terraza', 'DISPONIBLE'),
(9, 8, 'Terraza', 'DISPONIBLE'),
(10, 8, 'Terraza', 'DISPONIBLE');
GO
