const VERSION = 5;
const SIZE = VERSION * 4 + 17;
const DATA_CODEWORDS = 108;
const ECC_CODEWORDS = 26;
const TOTAL_CODEWORDS = DATA_CODEWORDS + ECC_CODEWORDS;

type Matrix = boolean[][];

const encoder = new TextEncoder();

export function createQrCodeMatrix(text: string): Matrix {
  const data = encodeData(text);
  const ecc = reedSolomonComputeRemainder(data, ECC_CODEWORDS);
  const codewords = [...data, ...ecc];
  const modules = createMatrix(false);
  const isFunction = createMatrix(false);

  drawFunctionPatterns(modules, isFunction);
  drawCodewords(modules, isFunction, codewords);

  return modules;
}

export function getQrCodeSize() {
  return SIZE;
}

function encodeData(text: string): number[] {
  const bytes = Array.from(encoder.encode(text));
  if (bytes.length > 106) {
    throw new Error('QR verisi cok uzun.');
  }

  const bits: number[] = [];
  appendBits(bits, 0b0100, 4);
  appendBits(bits, bytes.length, 8);
  bytes.forEach(byte => appendBits(bits, byte, 8));

  const maxBits = DATA_CODEWORDS * 8;
  appendBits(bits, 0, Math.min(4, maxBits - bits.length));
  while (bits.length % 8 !== 0) {
    bits.push(0);
  }

  const result: number[] = [];
  for (let i = 0; i < bits.length; i += 8) {
    result.push(bits.slice(i, i + 8).reduce((value, bit) => (value << 1) | bit, 0));
  }

  for (let pad = 0xec; result.length < DATA_CODEWORDS; pad ^= 0xec ^ 0x11) {
    result.push(pad);
  }

  return result;
}

function appendBits(bits: number[], value: number, length: number) {
  for (let i = length - 1; i >= 0; i--) {
    bits.push((value >>> i) & 1);
  }
}

function createMatrix(value: boolean): Matrix {
  return Array.from({ length: SIZE }, () => Array.from({ length: SIZE }, () => value));
}

function drawFunctionPatterns(modules: Matrix, isFunction: Matrix) {
  drawFinderPattern(modules, isFunction, 3, 3);
  drawFinderPattern(modules, isFunction, SIZE - 4, 3);
  drawFinderPattern(modules, isFunction, 3, SIZE - 4);
  drawAlignmentPattern(modules, isFunction, 30, 30);

  for (let i = 0; i < SIZE; i++) {
    if (!isFunction[6][i]) {
      setFunctionModule(modules, isFunction, i, 6, i % 2 === 0);
    }
    if (!isFunction[i][6]) {
      setFunctionModule(modules, isFunction, 6, i, i % 2 === 0);
    }
  }

  drawFormatBits(modules, isFunction);
  setFunctionModule(modules, isFunction, 8, SIZE - 8, true);
}

function drawFinderPattern(modules: Matrix, isFunction: Matrix, centerX: number, centerY: number) {
  for (let dy = -4; dy <= 4; dy++) {
    for (let dx = -4; dx <= 4; dx++) {
      const x = centerX + dx;
      const y = centerY + dy;
      if (x < 0 || x >= SIZE || y < 0 || y >= SIZE) {
        continue;
      }

      const distance = Math.max(Math.abs(dx), Math.abs(dy));
      setFunctionModule(modules, isFunction, x, y, distance !== 2 && distance !== 4);
    }
  }
}

function drawAlignmentPattern(modules: Matrix, isFunction: Matrix, centerX: number, centerY: number) {
  for (let dy = -2; dy <= 2; dy++) {
    for (let dx = -2; dx <= 2; dx++) {
      setFunctionModule(
        modules,
        isFunction,
        centerX + dx,
        centerY + dy,
        Math.max(Math.abs(dx), Math.abs(dy)) !== 1
      );
    }
  }
}

function drawFormatBits(modules: Matrix, isFunction: Matrix) {
  const bits = getFormatBits(0b01, 0);
  for (let i = 0; i <= 5; i++) {
    setFunctionModule(modules, isFunction, 8, i, getBit(bits, i));
  }
  setFunctionModule(modules, isFunction, 8, 7, getBit(bits, 6));
  setFunctionModule(modules, isFunction, 8, 8, getBit(bits, 7));
  setFunctionModule(modules, isFunction, 7, 8, getBit(bits, 8));
  for (let i = 9; i < 15; i++) {
    setFunctionModule(modules, isFunction, 14 - i, 8, getBit(bits, i));
  }

  for (let i = 0; i < 8; i++) {
    setFunctionModule(modules, isFunction, SIZE - 1 - i, 8, getBit(bits, i));
  }
  for (let i = 8; i < 15; i++) {
    setFunctionModule(modules, isFunction, 8, SIZE - 15 + i, getBit(bits, i));
  }
}

function getFormatBits(errorCorrectionLevel: number, mask: number) {
  const data = (errorCorrectionLevel << 3) | mask;
  let remainder = data << 10;
  const generator = 0x537;

  for (let i = 14; i >= 10; i--) {
    if (((remainder >>> i) & 1) !== 0) {
      remainder ^= generator << (i - 10);
    }
  }

  return ((data << 10) | remainder) ^ 0x5412;
}

function getBit(value: number, index: number) {
  return ((value >>> index) & 1) !== 0;
}

function setFunctionModule(modules: Matrix, isFunction: Matrix, x: number, y: number, value: boolean) {
  modules[y][x] = value;
  isFunction[y][x] = true;
}

function drawCodewords(modules: Matrix, isFunction: Matrix, codewords: number[]) {
  if (codewords.length !== TOTAL_CODEWORDS) {
    throw new Error('QR kod kelime sayisi gecersiz.');
  }

  let bitIndex = 0;
  let upward = true;
  const totalBits = codewords.length * 8;

  for (let right = SIZE - 1; right >= 1; right -= 2) {
    if (right === 6) {
      right--;
    }

    for (let vertical = 0; vertical < SIZE; vertical++) {
      const y = upward ? SIZE - 1 - vertical : vertical;
      for (let dx = 0; dx < 2; dx++) {
        const x = right - dx;
        if (isFunction[y][x]) {
          continue;
        }

        let bit = false;
        if (bitIndex < totalBits) {
          const codeword = codewords[Math.floor(bitIndex / 8)];
          bit = ((codeword >>> (7 - (bitIndex % 8))) & 1) !== 0;
          bitIndex++;
        }

        if ((x + y) % 2 === 0) {
          bit = !bit;
        }
        modules[y][x] = bit;
      }
    }

    upward = !upward;
  }
}

function reedSolomonComputeRemainder(data: number[], degree: number): number[] {
  const generator = reedSolomonGenerator(degree);
  const result = Array.from({ length: degree }, () => 0);

  for (const byte of data) {
    const factor = byte ^ result.shift()!;
    result.push(0);
    for (let i = 0; i < degree; i++) {
      result[i] ^= gfMultiply(generator[i + 1], factor);
    }
  }

  return result;
}

function reedSolomonGenerator(degree: number): number[] {
  let result = [1];
  for (let i = 0; i < degree; i++) {
    const next = [1, gfPow(i)];
    const product = Array.from({ length: result.length + 1 }, () => 0);

    result.forEach((coefficient, index) => {
      product[index] ^= gfMultiply(coefficient, next[0]);
      product[index + 1] ^= gfMultiply(coefficient, next[1]);
    });

    result = product;
  }

  return result;
}

function gfMultiply(x: number, y: number) {
  let result = 0;
  for (let i = 7; i >= 0; i--) {
    result = (result << 1) ^ (((result >>> 7) & 1) * 0x11d);
    result ^= ((y >>> i) & 1) * x;
  }

  return result & 0xff;
}

function gfPow(exponent: number) {
  let result = 1;
  for (let i = 0; i < exponent; i++) {
    result = gfMultiply(result, 2);
  }

  return result;
}
