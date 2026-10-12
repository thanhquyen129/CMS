import React, { useRef, useState } from "react";
import {
  Alert,
  PanResponder,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from "react-native";
import * as FileSystem from "expo-file-system/legacy";
import { createIdempotencyKey } from "@lcms/shared";

interface Point {
  x: number;
  y: number;
}

export interface SignatureCanvasProps {
  onSignatureCaptured: (result: { localUri: string; svgContent: string }) => void;
  height?: number;
}

export const SignatureCanvas: React.FC<SignatureCanvasProps> = ({
  onSignatureCaptured,
  height = 180,
}) => {
  const [paths, setPaths] = useState<Point[][]>([]);
  const currentPathRef = useRef<Point[]>([]);
  const [isSaved, setIsSaved] = useState<boolean>(false);

  const panResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onMoveShouldSetPanResponder: () => true,
      onPanResponderGrant: (evt) => {
        setIsSaved(false);
        const { locationX, locationY } = evt.nativeEvent;
        const newPoint = { x: Math.round(locationX), y: Math.round(locationY) };
        currentPathRef.current = [newPoint];
        setPaths((prev) => [...prev, [newPoint]]);
      },
      onPanResponderMove: (evt) => {
        const { locationX, locationY } = evt.nativeEvent;
        const newPoint = { x: Math.round(locationX), y: Math.round(locationY) };
        currentPathRef.current.push(newPoint);

        setPaths((prev) => {
          const next = [...prev];
          if (next.length > 0) {
            next[next.length - 1] = [...currentPathRef.current];
          }
          return next;
        });
      },
      onPanResponderRelease: () => {
        currentPathRef.current = [];
      },
    })
  ).current;

  const handleClear = () => {
    setPaths([]);
    currentPathRef.current = [];
    setIsSaved(false);
  };

  /**
   * Chuyển đổi các nét vẽ thành mã SVG Vector chuẩn dung lượng siêu nhỏ (< 15KB).
   */
  const exportSignatureToSvg = async () => {
    if (paths.length === 0) {
      Alert.alert("Chưa có chữ ký", "Vui lòng ký tên vào khung trước khi xác nhận.");
      return;
    }

    const width = 360;
    let pathD = "";

    for (const stroke of paths) {
      if (stroke.length === 0) continue;
      pathD += ` M ${stroke[0].x} ${stroke[0].y}`;
      for (let i = 1; i < stroke.length; i++) {
        pathD += ` L ${stroke[i].x} ${stroke[i].y}`;
      }
    }

    const svgContent = `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${width} ${height}" width="${width}" height="${height}">
  <rect width="100%" height="100%" fill="#FFFFFF"/>
  <path d="${pathD.trim()}" stroke="#0F172A" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" fill="none"/>
</svg>`;

    try {
      const docDir = FileSystem.documentDirectory || "";
      const targetDir = `${docDir}lcms_offline_attachments/`;
      const dirInfo = await FileSystem.getInfoAsync(targetDir);
      if (!dirInfo.exists) {
        await FileSystem.makeDirectoryAsync(targetDir, { intermediates: true });
      }

      const fileName = `pod_sign_${Date.now()}_${createIdempotencyKey("sig")}.svg`;
      const fileUri = `${targetDir}${fileName}`;

      await FileSystem.writeAsStringAsync(fileUri, svgContent, {
        encoding: FileSystem.EncodingType.UTF8,
      });

      setIsSaved(true);
      onSignatureCaptured({ localUri: fileUri, svgContent });
    } catch (err) {
      Alert.alert("Lỗi lưu chữ ký", err instanceof Error ? err.message : "Không thể lưu tệp chữ ký.");
    }
  };

  return (
    <View style={styles.container}>
      <View style={[styles.canvasBox, { height }]} {...panResponder.panHandlers}>
        <Text style={styles.watermarkText}>Ký tên bằng ngón tay vào đây</Text>
        <View style={styles.baseline} />

        {/* Vẽ các nét chữ ký */}
        {paths.map((stroke, sIdx) =>
          stroke.map((pt, pIdx) => (
            <View
              key={`${sIdx}-${pIdx}`}
              style={[
                styles.dot,
                {
                  left: pt.x - 2,
                  top: pt.y - 2,
                },
              ]}
            />
          ))
        )}
      </View>

      {/* Nút thao tác dưới Canvas */}
      <View style={styles.actionRow}>
        <TouchableOpacity style={styles.clearBtn} onPress={handleClear}>
          <Text style={styles.clearBtnText}>✕ Xóa ký lại</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.confirmBtn, isSaved && styles.confirmBtnSaved]}
          onPress={exportSignatureToSvg}
        >
          <Text style={styles.confirmBtnText}>
            {isSaved ? "✓ Đã chốt chữ ký (< 15KB)" : "✓ Chốt chữ ký"}
          </Text>
        </TouchableOpacity>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginVertical: 8,
  },
  canvasBox: {
    backgroundColor: "#FFFFFF",
    borderRadius: 12,
    borderWidth: 1.5,
    borderColor: "#94A3B8",
    borderStyle: "dashed",
    position: "relative",
    overflow: "hidden",
  },
  watermarkText: {
    position: "absolute",
    top: 12,
    left: 14,
    fontSize: 11,
    color: "#94A3B8",
    fontWeight: "600",
  },
  baseline: {
    position: "absolute",
    bottom: 35,
    left: 20,
    right: 20,
    height: 1,
    backgroundColor: "#E2E8F0",
  },
  dot: {
    position: "absolute",
    width: 4.5,
    height: 4.5,
    borderRadius: 2.25,
    backgroundColor: "#0F172A",
  },
  actionRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginTop: 8,
    gap: 10,
  },
  clearBtn: {
    backgroundColor: "#F1F5F9",
    borderWidth: 1,
    borderColor: "#CBD5E1",
    paddingHorizontal: 14,
    paddingVertical: 9,
    borderRadius: 8,
  },
  clearBtnText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#475569",
  },
  confirmBtn: {
    flex: 1,
    backgroundColor: "#2563EB",
    paddingVertical: 9,
    borderRadius: 8,
    alignItems: "center",
  },
  confirmBtnSaved: {
    backgroundColor: "#059669",
  },
  confirmBtnText: {
    fontSize: 12,
    fontWeight: "700",
    color: "#FFFFFF",
  },
});
