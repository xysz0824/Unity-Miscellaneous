
			//获取头发高光
			fixed StrandSpecular ( fixed3 T, fixed3 V, fixed3 L, fixed exponent)
			{
				fixed3 H = normalize(L + V);
				fixed dotTH = dot(T, H);
				fixed sinTH = sqrt(1-dotTH * dotTH);
				fixed dirAtten = smoothstep(-1, 0, dotTH);
				return  dirAtten*pow(sinTH, exponent);
			}
			
			//沿着法线方向调整Tangent方向
			fixed3 ShiftTangent ( fixed3 T, fixed3 N, fixed shift)
			{
				return normalize(T + shift * N);
			}